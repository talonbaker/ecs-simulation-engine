using System;
using System.Linq;
using APIFramework.Bootstrap;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Bootstrap;

/// <summary>
/// Unit tests for CastGenerator.
///
/// AT-01 — archetypes.json validates and contains all ten cast-bible archetypes.
/// AT-02 — SpawnNpc on the-vent produces Belonging.Baseline in the elevated range [55, 75].
/// AT-03 — Two SpawnNpc calls with the same seed produce byte-identical components.
/// AT-04 — Spawned NPC has NpcTag; no NpcSlotTag.
/// AT-05 — The-affair's inhibitions match count (2) and class set (Infidelity + Vulnerability).
/// AT-06 — SeedRelationships with 10 plain NPCs produces the expected pattern counts.
/// AT-07 — The-affair archetype seeds an activeAffair relationship.
/// AT-08 — The-crush archetype seeds a secretCrush relationship toward the crush NPC.
/// AT-11 — All NPCs spawned from SpawnAll have unique entity ids.
/// </summary>
public class CastGeneratorTests
{
    private static readonly CastGeneratorConfig Cfg = new();

    private static ArchetypeCatalog LoadCatalog()
        => ArchetypeCatalog.LoadDefault()
           ?? throw new InvalidOperationException("Could not locate archetypes.json.");

    private static NpcSlotComponent Slot(int x = 5, int z = 5, string? hint = null)
        => new NpcSlotComponent { X = x, Y = z, ArchetypeHint = hint };

    // -- AT-01: catalog loads all ten archetypes -------------------------------

    [Fact]
    public void LoadDefault_ReturnsAllTenArchetypes()
    {
        var catalog = LoadCatalog();
        Assert.Equal(10, catalog.AllArchetypes.Count);
    }

    [Theory]
    [InlineData("the-vent")]
    [InlineData("the-hermit")]
    [InlineData("the-climber")]
    [InlineData("the-cynic")]
    [InlineData("the-newbie")]
    [InlineData("the-old-hand")]
    [InlineData("the-affair")]
    [InlineData("the-recovering")]
    [InlineData("the-founders-nephew")]
    [InlineData("the-crush")]
    public void LoadDefault_EachCastBibleArchetypePresent(string id)
    {
        var catalog = LoadCatalog();
        Assert.NotNull(catalog.TryGet(id));
    }

    // -- AT-02: the-vent elevated drive baseline -------------------------------

    [Fact]
    public void SpawnNpc_TheVent_BelongingBaseline_IsInElevatedRange()
    {
        var catalog   = LoadCatalog();
        var archetype = catalog.TryGet("the-vent")!;
        var em        = new EntityManager();
        var rng       = new SeededRandom(42);

        var npc    = CastGenerator.SpawnNpc(archetype, Slot(), em, rng, Cfg);
        var drives = npc.Get<SocialDrivesComponent>();

        Assert.InRange(drives.Belonging.Baseline, Cfg.ElevatedDriveRange[0], Cfg.ElevatedDriveRange[1]);
    }

    [Fact]
    public void SpawnNpc_TheVent_IrritationBaseline_IsInElevatedRange()
    {
        var catalog   = LoadCatalog();
        var archetype = catalog.TryGet("the-vent")!;
        var em        = new EntityManager();
        var rng       = new SeededRandom(7);

        var npc    = CastGenerator.SpawnNpc(archetype, Slot(), em, rng, Cfg);
        var drives = npc.Get<SocialDrivesComponent>();

        Assert.InRange(drives.Irritation.Baseline, Cfg.ElevatedDriveRange[0], Cfg.ElevatedDriveRange[1]);
    }

    [Fact]
    public void SpawnNpc_TheHermit_TrustBaseline_IsInDepressedRange()
    {
        var catalog   = LoadCatalog();
        var archetype = catalog.TryGet("the-hermit")!;
        var em        = new EntityManager();
        var rng       = new SeededRandom(13);

        var npc    = CastGenerator.SpawnNpc(archetype, Slot(), em, rng, Cfg);
        var drives = npc.Get<SocialDrivesComponent>();

        Assert.InRange(drives.Trust.Baseline, Cfg.DepressedDriveRange[0], Cfg.DepressedDriveRange[1]);
    }

    // -- AT-03: determinism ----------------------------------------------------

    [Fact]
    public void SpawnNpc_SameSeed_ProducesByteIdenticalDrives()
    {
        var catalog   = LoadCatalog();
        var archetype = catalog.TryGet("the-vent")!;

        var em1 = new EntityManager(); var rng1 = new SeededRandom(99);
        var em2 = new EntityManager(); var rng2 = new SeededRandom(99);

        var npc1 = CastGenerator.SpawnNpc(archetype, Slot(), em1, rng1, Cfg);
        var npc2 = CastGenerator.SpawnNpc(archetype, Slot(), em2, rng2, Cfg);

        var d1 = npc1.Get<SocialDrivesComponent>();
        var d2 = npc2.Get<SocialDrivesComponent>();

        Assert.Equal(d1.Belonging.Baseline,  d2.Belonging.Baseline);
        Assert.Equal(d1.Status.Baseline,     d2.Status.Baseline);
        Assert.Equal(d1.Affection.Baseline,  d2.Affection.Baseline);
        Assert.Equal(d1.Irritation.Baseline, d2.Irritation.Baseline);
        Assert.Equal(d1.Trust.Baseline,      d2.Trust.Baseline);
        Assert.Equal(d1.Loneliness.Baseline, d2.Loneliness.Baseline);

        var p1 = npc1.Get<PersonalityComponent>();
        var p2 = npc2.Get<PersonalityComponent>();
        Assert.Equal(p1.Openness,          p2.Openness);
        Assert.Equal(p1.Conscientiousness, p2.Conscientiousness);
        Assert.Equal(p1.Extraversion,      p2.Extraversion);
        Assert.Equal(p1.VocabularyRegister,p2.VocabularyRegister);

        Assert.Equal(npc1.Get<WillpowerComponent>().Baseline, npc2.Get<WillpowerComponent>().Baseline);
    }

    // -- AT-04: NpcTag present, NpcSlotTag absent ------------------------------

    [Fact]
    public void SpawnNpc_ResultEntity_HasNpcTag()
    {
        var catalog   = LoadCatalog();
        var archetype = catalog.TryGet("the-newbie")!;
        var em        = new EntityManager();
        var rng       = new SeededRandom(1);

        var npc = CastGenerator.SpawnNpc(archetype, Slot(), em, rng, Cfg);

        Assert.True(npc.Has<NpcTag>(), "Spawned NPC must have NpcTag.");
    }

    [Fact]
    public void SpawnNpc_ResultEntity_HasNoNpcSlotTag()
    {
        var catalog   = LoadCatalog();
        var archetype = catalog.TryGet("the-newbie")!;
        var em        = new EntityManager();
        var rng       = new SeededRandom(1);

        var npc = CastGenerator.SpawnNpc(archetype, Slot(), em, rng, Cfg);

        Assert.False(npc.Has<NpcSlotTag>(), "Spawned NPC must not have NpcSlotTag.");
    }

    [Fact]
    public void SpawnNpc_ResultEntity_HasAllSocialComponents()
    {
        var catalog   = LoadCatalog();
        var archetype = catalog.TryGet("the-climber")!;
        var em        = new EntityManager();
        var rng       = new SeededRandom(5);

        var npc = CastGenerator.SpawnNpc(archetype, Slot(), em, rng, Cfg);

        Assert.True(npc.Has<SocialDrivesComponent>());
        Assert.True(npc.Has<WillpowerComponent>());
        Assert.True(npc.Has<PersonalityComponent>());
        Assert.True(npc.Has<InhibitionsComponent>());
        Assert.True(npc.Has<SilhouetteComponent>());
        Assert.True(npc.Has<NpcArchetypeComponent>());
        Assert.True(npc.Has<NpcDealComponent>());
    }

    // -- AT-05: inhibitions match archetype starter set ------------------------

    [Fact]
    public void SpawnNpc_TheAffair_HasTwoInhibitions()
    {
        var catalog   = LoadCatalog();
        var archetype = catalog.TryGet("the-affair")!;
        var em        = new EntityManager();
        var rng       = new SeededRandom(3);

        var npc         = CastGenerator.SpawnNpc(archetype, Slot(), em, rng, Cfg);
        var inhibitions = npc.Get<InhibitionsComponent>().Inhibitions;

        Assert.Equal(2, inhibitions.Count);
    }

    [Fact]
    public void SpawnNpc_TheAffair_HasInfidelityAndVulnerabilityInhibitions()
    {
        var catalog   = LoadCatalog();
        var archetype = catalog.TryGet("the-affair")!;
        var em        = new EntityManager();
        var rng       = new SeededRandom(3);

        var npc         = CastGenerator.SpawnNpc(archetype, Slot(), em, rng, Cfg);
        var inhibitions = npc.Get<InhibitionsComponent>().Inhibitions;
        var classes     = inhibitions.Select(i => i.Class).ToList();

        Assert.Contains(InhibitionClass.Infidelity,   classes);
        Assert.Contains(InhibitionClass.Vulnerability, classes);
    }

    [Fact]
    public void SpawnNpc_TheAffair_InfidelityInhibitionStrength_InExpectedRange()
    {
        var catalog   = LoadCatalog();
        var archetype = catalog.TryGet("the-affair")!;
        var em        = new EntityManager();
        var rng       = new SeededRandom(3);

        var npc        = CastGenerator.SpawnNpc(archetype, Slot(), em, rng, Cfg);
        var infidelity = npc.Get<InhibitionsComponent>().Inhibitions
                            .First(i => i.Class == InhibitionClass.Infidelity);

        Assert.InRange(infidelity.Strength, 10, 30);
    }

    // -- AT-06: SeedRelationships pattern counts -------------------------------

    [Fact]
    public void SeedRelationships_TenPlainNpcs_ProducesExpectedPatternCounts()
    {
        var catalog = LoadCatalog();
        var neutral  = catalog.TryGet("the-newbie")!;
        var em       = new EntityManager();
        var rng      = new SeededRandom(77);

        // Spawn 10 plain NPCs (no relationshipSpawnHints)
        var npcs = Enumerable.Range(0, 10)
            .Select(_ => CastGenerator.SpawnNpc(neutral, Slot(), em, rng, Cfg))
            .ToList();

        var rels = CastGenerator.SeedRelationships(npcs, catalog, em, rng, Cfg);

        int Count(RelationshipPattern p) =>
            rels.Count(e => e.Get<RelationshipComponent>().Patterns.Contains(p));

        Assert.Equal(Cfg.RivalryCount,                  Count(RelationshipPattern.Rival));
        Assert.Equal(Cfg.OldFlameCount,                 Count(RelationshipPattern.OldFlame));
        Assert.Equal(Cfg.MentorPairCount,               Count(RelationshipPattern.Mentor));
        Assert.Equal(Cfg.SleptWithSpouseCount,          Count(RelationshipPattern.SleptWithSpouse));
        Assert.Equal(Cfg.FriendPairCount,               Count(RelationshipPattern.Friend));
        Assert.Equal(Cfg.ThingNobodyTalksAboutCount,    Count(RelationshipPattern.TheThingNobodyTalksAbout));
    }

    [Fact]
    public void SeedRelationships_FewerThanTwoNpcs_ReturnsEmpty()
    {
        var catalog  = LoadCatalog();
        var archetype = catalog.TryGet("the-newbie")!;
        var em        = new EntityManager();
        var rng       = new SeededRandom(1);

        var single = CastGenerator.SpawnNpc(archetype, Slot(), em, rng, Cfg);
        var rels   = CastGenerator.SeedRelationships(new[] { single }, catalog, em, rng, Cfg);

        Assert.Empty(rels);
    }

    // -- AT-07: the-affair seeds activeAffair ---------------------------------

    [Fact]
    public void SeedRelationships_TheAffairNpc_SeedsActiveAffairRelationship()
    {
        var catalog  = LoadCatalog();
        var affair   = catalog.TryGet("the-affair")!;
        var neutral  = catalog.TryGet("the-newbie")!;
        var em       = new EntityManager();
        var rng      = new SeededRandom(42);

        var affairNpc = CastGenerator.SpawnNpc(affair, Slot(), em, rng, Cfg);
        var others    = Enumerable.Range(0, 5)
            .Select(_ => CastGenerator.SpawnNpc(neutral, Slot(), em, rng, Cfg))
            .ToList();
        var all = new[] { affairNpc }.Concat(others).ToList();

        var rels = CastGenerator.SeedRelationships(all, catalog, em, rng, Cfg);

        bool hasAffair = rels.Any(e =>
            e.Get<RelationshipComponent>().Patterns.Contains(RelationshipPattern.ActiveAffair));

        Assert.True(hasAffair, "Expected at least one ActiveAffair relationship from the-affair archetype.");
    }

    // -- AT-08: the-crush seeds secretCrush -----------------------------------

    [Fact]
    public void SeedRelationships_TheCrushNpc_SeedsSecretCrushRelationship()
    {
        var catalog  = LoadCatalog();
        var crush    = catalog.TryGet("the-crush")!;
        var neutral  = catalog.TryGet("the-newbie")!;
        var em       = new EntityManager();
        var rng      = new SeededRandom(42);

        var crushNpc = CastGenerator.SpawnNpc(crush, Slot(), em, rng, Cfg);
        var others   = Enumerable.Range(0, 5)
            .Select(_ => CastGenerator.SpawnNpc(neutral, Slot(), em, rng, Cfg))
            .ToList();
        var all = new[] { crushNpc }.Concat(others).ToList();

        var rels = CastGenerator.SeedRelationships(all, catalog, em, rng, Cfg);

        bool hasCrush = rels.Any(e =>
            e.Get<RelationshipComponent>().Patterns.Contains(RelationshipPattern.SecretCrush));

        Assert.True(hasCrush, "Expected at least one SecretCrush relationship from the-crush archetype.");
    }

    // -- AT-11: SpawnAll → unique entity ids ----------------------------------

    [Fact]
    public void SpawnAll_AllSpawnedNpcs_HaveUniqueEntityIds()
    {
        var catalog = LoadCatalog();
        var em      = new EntityManager();
        var rng     = new SeededRandom(1);

        // Seed the EM with NPC slot entities matching the-vent archetype
        for (int i = 0; i < 8; i++)
        {
            var slot = em.CreateEntity();
            slot.Add(new NpcSlotTag());
            slot.Add(new NpcSlotComponent { X = i * 2, Y = 0, ArchetypeHint = "the-vent" });
        }

        var npcs = CastGenerator.SpawnAll(catalog, em, rng, Cfg);

        var ids = npcs.Select(e => e.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void SpawnAll_DestroysSlotEntities()
    {
        var catalog = LoadCatalog();
        var em      = new EntityManager();
        var rng     = new SeededRandom(2);

        var slot = em.CreateEntity();
        slot.Add(new NpcSlotTag());
        slot.Add(new NpcSlotComponent { X = 1, Y = 0, ArchetypeHint = "the-newbie" });

        CastGenerator.SpawnAll(catalog, em, rng, Cfg);

        Assert.Empty(em.Query<NpcSlotTag>());
    }
}
