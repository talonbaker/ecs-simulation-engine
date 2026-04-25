using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using Warden.Contracts;
using Warden.Contracts.SchemaValidation;
using Warden.Contracts.Telemetry;
using Warden.Telemetry;
using Xunit;
using WcVocabRegister       = Warden.Contracts.Telemetry.VocabularyRegister;
using WcInhibitionClass     = Warden.Contracts.Telemetry.InhibitionClass;
using WcInhibitionAwareness = Warden.Contracts.Telemetry.InhibitionAwareness;
using WcRelationshipPattern = Warden.Contracts.Telemetry.RelationshipPattern;

namespace Warden.Telemetry.Tests;

/// <summary>
/// Acceptance tests for <see cref="TelemetryProjector"/>:
///
/// AT-01 — Projector emits SchemaVersion = "0.2.1".
/// AT-02 — NPC with full social state projects all fields correctly.
/// AT-03 — Non-NPC entity has social absent (null).
/// AT-04 — RelationshipTag entity produces a relationships[] entry.
/// AT-05 — Relationships sorted by id ascending.
/// AT-06 — Projected DTO with social state validates against world-state.schema.json.
/// AT-07 — Two projections of the same snapshot + same inputs → byte-identical JSON.
/// AT-08 — All previous projector tests still pass.
/// </summary>
public class TelemetryProjectorTests
{
    // ── Shared fixture helpers ────────────────────────────────────────────────

    private static SimulationBootstrapper MakeSim(int humanCount = 1)
        => new(new InMemoryConfigProvider(new SimConfig()), humanCount);

    private static readonly DateTimeOffset FixedCapture =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static WorldStateDto Capture(
        SimulationBootstrapper sim,
        long           tick       = 0,
        int            seed       = 42,
        string         simVersion = "test-0.0.1")
    {
        var snap = sim.Capture();
        return TelemetryProjector.Project(
            snap, sim.EntityManager, FixedCapture, tick, seed, simVersion);
    }

    // ── AT-01: SchemaVersion ─────────────────────────────────────────────────

    [Fact]
    public void AT01_SchemaVersion_Is021()
    {
        var dto = Capture(MakeSim());
        Assert.Equal("0.2.1", dto.SchemaVersion);
    }

    // ── AT-02: NPC social projection ─────────────────────────────────────────

    [Fact]
    public void AT02_NpcWithFullSocialState_ProjectsAllFields()
    {
        var sim = MakeSim(humanCount: 0);
        var npc = EntityTemplates.SpawnHuman(sim.EntityManager);
        EntityTemplates.WithSocial(npc,
            drives: new SocialDrivesComponent
            {
                Belonging  = new DriveValue { Current = 60, Baseline = 50 },
                Status     = new DriveValue { Current = 40, Baseline = 45 },
                Affection  = new DriveValue { Current = 70, Baseline = 65 },
                Irritation = new DriveValue { Current = 20, Baseline = 25 },
                Attraction = new DriveValue { Current = 55, Baseline = 50 },
                Trust      = new DriveValue { Current = 80, Baseline = 75 },
                Suspicion  = new DriveValue { Current = 30, Baseline = 35 },
                Loneliness = new DriveValue { Current = 45, Baseline = 50 },
            },
            willpower: new WillpowerComponent(80, 70),
            personality: new PersonalityComponent(
                openness: 1, conscientiousness: -1, extraversion: 2,
                agreeableness: 0, neuroticism: -2,
                register: APIFramework.Components.VocabularyRegister.Casual,
                currentMood: "content"),
            inhibitions: new InhibitionsComponent(new List<Inhibition>
            {
                new(APIFramework.Components.InhibitionClass.Confrontation, 60, APIFramework.Components.InhibitionAwareness.Known),
                new(APIFramework.Components.InhibitionClass.Vulnerability,  40, APIFramework.Components.InhibitionAwareness.Hidden),
            }));

        var snap = sim.Capture();
        var dto  = TelemetryProjector.Project(snap, sim.EntityManager, FixedCapture, 0, 42, "test");

        var entity = dto.Entities.First(e => e.Id == npc.Id.ToString());
        var social = entity.Social;

        Assert.NotNull(social);

        // Drives
        Assert.NotNull(social.Drives);
        Assert.Equal(60, social.Drives.Belonging.Current);
        Assert.Equal(50, social.Drives.Belonging.Baseline);
        Assert.Equal(40, social.Drives.Status.Current);
        Assert.Equal(70, social.Drives.Affection.Current);
        Assert.Equal(20, social.Drives.Irritation.Current);
        Assert.Equal(55, social.Drives.Attraction.Current);
        Assert.Equal(80, social.Drives.Trust.Current);
        Assert.Equal(30, social.Drives.Suspicion.Current);
        Assert.Equal(45, social.Drives.Loneliness.Current);

        // Willpower
        Assert.NotNull(social.Willpower);
        Assert.Equal(80, social.Willpower.Current);
        Assert.Equal(70, social.Willpower.Baseline);

        // Personality traits (all 5)
        Assert.NotNull(social.PersonalityTraits);
        Assert.Equal(5, social.PersonalityTraits.Count);
        Assert.Equal(1,  social.PersonalityTraits.First(t => t.Dimension == BigFiveDimension.Openness).Value);
        Assert.Equal(-1, social.PersonalityTraits.First(t => t.Dimension == BigFiveDimension.Conscientiousness).Value);
        Assert.Equal(2,  social.PersonalityTraits.First(t => t.Dimension == BigFiveDimension.Extraversion).Value);
        Assert.Equal(0,  social.PersonalityTraits.First(t => t.Dimension == BigFiveDimension.Agreeableness).Value);
        Assert.Equal(-2, social.PersonalityTraits.First(t => t.Dimension == BigFiveDimension.Neuroticism).Value);

        // Mood and vocab register
        Assert.Equal("content", social.CurrentMood);
        Assert.Equal(WcVocabRegister.Casual, social.VocabularyRegister);

        // Inhibitions
        Assert.NotNull(social.Inhibitions);
        Assert.Equal(2, social.Inhibitions.Count);
        Assert.Contains(social.Inhibitions,
            i => i.Class == WcInhibitionClass.Confrontation && i.Strength == 60 && i.Awareness == WcInhibitionAwareness.Known);
        Assert.Contains(social.Inhibitions,
            i => i.Class == WcInhibitionClass.Vulnerability  && i.Strength == 40 && i.Awareness == WcInhibitionAwareness.Hidden);
    }

    // ── AT-03: Non-NPC has social absent ─────────────────────────────────────

    [Fact]
    public void AT03_NonNpcEntity_SocialIsAbsent()
    {
        // SpawnHuman does not add NpcTag — social must be null.
        var sim = MakeSim(humanCount: 1);
        var dto = Capture(sim);

        Assert.All(dto.Entities, e => Assert.Null(e.Social));
    }

    // ── AT-04: Relationship projection ───────────────────────────────────────

    [Fact]
    public void AT04_RelationshipTagEntity_ProducesRelationshipEntry()
    {
        // Use an independent EntityManager so participant counter values are known.
        var em = new EntityManager();
        var entityA = em.CreateEntity(); // counter = 1
        var entityB = em.CreateEntity(); // counter = 2
        var relEntity = em.CreateEntity(); // counter = 3
        relEntity.Add(new RelationshipTag());
        relEntity.Add(new RelationshipComponent(1, 2,
            new List<APIFramework.Components.RelationshipPattern>
                { APIFramework.Components.RelationshipPattern.Friend },
            intensity: 75));

        // Project against an empty snapshot (no living entities needed).
        var snap = MakeSim(humanCount: 0).Capture();
        var dto  = TelemetryProjector.Project(snap, em, FixedCapture, 0, 0, "test");

        Assert.NotNull(dto.Relationships);
        Assert.Single(dto.Relationships);

        var rel = dto.Relationships[0];
        Assert.Equal(relEntity.Id.ToString(), rel.Id);
        Assert.Equal(entityA.Id.ToString(), rel.ParticipantA);
        Assert.Equal(entityB.Id.ToString(), rel.ParticipantB);
        Assert.Single(rel.Patterns);
        Assert.Equal(WcRelationshipPattern.Friend, rel.Patterns[0]);
        Assert.Equal(75, rel.Intensity);
        Assert.Empty(rel.HistoryEventIds);
    }

    // ── AT-05: Relationships sorted by Id ascending ───────────────────────────

    [Fact]
    public void AT05_Relationships_SortedByIdAscending()
    {
        var em = new EntityManager();
        // Create two relationship entities — first created gets smaller Guid.
        var relFirst  = em.CreateEntity();
        relFirst.Add(new RelationshipTag());
        relFirst.Add(new RelationshipComponent(1, 2));

        var relSecond = em.CreateEntity();
        relSecond.Add(new RelationshipTag());
        relSecond.Add(new RelationshipComponent(1, 3));

        var snap = MakeSim(humanCount: 0).Capture();
        var dto  = TelemetryProjector.Project(snap, em, FixedCapture, 0, 0, "test");

        Assert.NotNull(dto.Relationships);
        Assert.Equal(2, dto.Relationships.Count);

        // IDs are counter-based Guids; lexicographic order matches counter order.
        var ids = dto.Relationships.Select(r => r.Id).ToList();
        Assert.Equal(ids.OrderBy(x => x, StringComparer.Ordinal).ToList(), ids);
    }

    // ── AT-06: Schema validation with social state ────────────────────────────

    [Fact]
    public void AT06_NpcWithSocialState_ValidatesAgainstSchema()
    {
        var sim = MakeSim(humanCount: 0);
        var npc = EntityTemplates.SpawnHuman(sim.EntityManager);
        EntityTemplates.WithSocial(npc,
            drives: new SocialDrivesComponent
            {
                Belonging  = new DriveValue { Current = 55, Baseline = 50 },
                Status     = new DriveValue { Current = 50, Baseline = 50 },
                Affection  = new DriveValue { Current = 50, Baseline = 50 },
                Irritation = new DriveValue { Current = 25, Baseline = 25 },
                Attraction = new DriveValue { Current = 50, Baseline = 50 },
                Trust      = new DriveValue { Current = 60, Baseline = 60 },
                Suspicion  = new DriveValue { Current = 40, Baseline = 40 },
                Loneliness = new DriveValue { Current = 45, Baseline = 50 },
            },
            willpower:   new WillpowerComponent(75, 75),
            personality: new PersonalityComponent(0, 0, 0, 0, 0,
                APIFramework.Components.VocabularyRegister.Casual, "okay"),
            inhibitions: new InhibitionsComponent(new List<Inhibition>
            {
                new(APIFramework.Components.InhibitionClass.Confrontation, 50, APIFramework.Components.InhibitionAwareness.Known),
            }));

        var snap   = sim.Capture();
        var dto    = TelemetryProjector.Project(snap, sim.EntityManager, FixedCapture, 0, 42, "test");
        var json   = TelemetrySerializer.SerializeSnapshot(dto);
        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.True(result.IsValid,
            $"Schema validation failed: {string.Join("; ", result.Errors)}\n\nJSON:\n{json}");
    }

    // ── AT-07: Determinism with social state ─────────────────────────────────

    [Fact]
    public void AT07_SameInputsWithSocial_ProduceBytIdenticalJson()
    {
        var sim = MakeSim(humanCount: 0);
        var npc = EntityTemplates.SpawnHuman(sim.EntityManager);
        EntityTemplates.WithSocial(npc,
            willpower: new WillpowerComponent(60, 60));

        var snap = sim.Capture();

        var json1 = TelemetrySerializer.SerializeSnapshot(
            TelemetryProjector.Project(snap, sim.EntityManager, FixedCapture, 5L, 99, "v1"));
        var json2 = TelemetrySerializer.SerializeSnapshot(
            TelemetryProjector.Project(snap, sim.EntityManager, FixedCapture, 5L, 99, "v1"));

        Assert.Equal(json1, json2);
    }

    // ── AT-08: Existing tests (unchanged below this line) ────────────────────

    [Fact]
    public void AT01_FreshSim_ProjectValidatesAgainstSchema()
    {
        var sim  = MakeSim();
        var dto  = Capture(sim);
        var json = TelemetrySerializer.SerializeSnapshot(dto);

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.True(result.IsValid,
            $"world-state schema validation failed: {string.Join("; ", result.Errors)}\n\nJSON:\n{json}");
    }

    [Fact]
    public void AT01_ZeroHumanSim_ProjectValidatesAgainstSchema()
    {
        var sim  = MakeSim(humanCount: 0);
        var dto  = Capture(sim);
        var json = TelemetrySerializer.SerializeSnapshot(dto);

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.True(result.IsValid,
            $"world-state schema validation failed (0 humans): {string.Join("; ", result.Errors)}");
    }

    [Fact]
    public void AT02_SameInputs_ProduceBytIdenticalJson()
    {
        var sim   = MakeSim();
        var snap  = sim.Capture();

        var dto1  = TelemetryProjector.Project(snap, sim.EntityManager, FixedCapture, 7L, 99, "v1");
        var dto2  = TelemetryProjector.Project(snap, sim.EntityManager, FixedCapture, 7L, 99, "v1");

        var json1 = TelemetrySerializer.SerializeSnapshot(dto1);
        var json2 = TelemetrySerializer.SerializeSnapshot(dto2);

        Assert.Equal(json1, json2);
    }

    [Fact]
    public void AT02_DifferentTick_ProducesDifferentJson()
    {
        var sim  = MakeSim();
        var snap = sim.Capture();

        var json1 = TelemetrySerializer.SerializeSnapshot(
            TelemetryProjector.Project(snap, sim.EntityManager, FixedCapture, 0L, 1, "v1"));
        var json2 = TelemetrySerializer.SerializeSnapshot(
            TelemetryProjector.Project(snap, sim.EntityManager, FixedCapture, 1L, 1, "v1"));

        Assert.NotEqual(json1, json2);
    }

    [Fact]
    public void AT03_HumanAndCat_SpeciesResolvesCorrectly()
    {
        var sim = MakeSim(humanCount: 0);

        var humanEntity = EntityTemplates.SpawnHuman(sim.EntityManager);
        var catEntity   = EntityTemplates.SpawnCat(sim.EntityManager);

        var snap = sim.Capture();
        var dto  = TelemetryProjector.Project(
            snap, sim.EntityManager, FixedCapture, 0L, 0, "test");

        var humanDto = dto.Entities.First(e => e.Id == humanEntity.Id.ToString());
        var catDto   = dto.Entities.First(e => e.Id == catEntity.Id.ToString());

        Assert.Equal(SpeciesType.Human, humanDto.Species);
        Assert.Equal(SpeciesType.Cat,   catDto.Species);
    }

    [Fact]
    public void AT03_NoEntityManager_SpeciesFallsBackToUnknown()
    {
        var sim  = MakeSim(humanCount: 1);
        var snap = sim.Capture();

        var dto = TelemetryProjector.Project(snap, FixedCapture, 0L, 0, "test");

        Assert.All(dto.Entities, e =>
            Assert.Equal(SpeciesType.Unknown, e.Species));
    }

    [Fact]
    public void SerializeFrame_AppendsNewline()
    {
        var sim  = MakeSim(humanCount: 0);
        var dto  = Capture(sim);
        var line = TelemetrySerializer.SerializeFrame(dto);

        Assert.EndsWith("\n", line);
        Assert.DoesNotContain("\n", line.TrimEnd('\n'));
    }

    [Fact]
    public void SerializeSnapshot_AndFrame_SameContent()
    {
        var sim      = MakeSim(humanCount: 0);
        var dto      = Capture(sim);
        var snapshot = TelemetrySerializer.SerializeSnapshot(dto);
        var frame    = TelemetrySerializer.SerializeFrame(dto);

        Assert.Equal(snapshot, frame.TrimEnd('\n'));
    }
}
