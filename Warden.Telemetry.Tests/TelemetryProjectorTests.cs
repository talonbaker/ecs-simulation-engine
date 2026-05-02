using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Chronicle;
using APIFramework.Systems.Lighting;
using Warden.Contracts;
using Warden.Contracts.SchemaValidation;
using Warden.Contracts.Telemetry;
using Warden.Telemetry;
using Xunit;
using WcVocabRegister       = Warden.Contracts.Telemetry.VocabularyRegister;
using WcInhibitionClass     = Warden.Contracts.Telemetry.InhibitionClass;
using WcInhibitionAwareness = Warden.Contracts.Telemetry.InhibitionAwareness;
using WcRelationshipPattern = Warden.Contracts.Telemetry.RelationshipPattern;
using WcDayPhase            = Warden.Contracts.Telemetry.DayPhase;
using WcRoomCategory        = Warden.Contracts.Telemetry.RoomCategory;
using WcBuildingFloor       = Warden.Contracts.Telemetry.BuildingFloor;
using WcLightKind           = Warden.Contracts.Telemetry.LightKind;
using WcLightState          = Warden.Contracts.Telemetry.LightState;
using WcApertureFacing      = Warden.Contracts.Telemetry.ApertureFacing;
using EngRoomCategory       = APIFramework.Components.RoomCategory;
using EngBuildingFloor      = APIFramework.Components.BuildingFloor;
using EngLightKind          = APIFramework.Components.LightKind;
using EngLightState         = APIFramework.Components.LightState;
using EngApertureFacing     = APIFramework.Components.ApertureFacing;
using EngDayPhase           = APIFramework.Components.DayPhase;

namespace Warden.Telemetry.Tests;

/// <summary>
/// Acceptance tests for <see cref="TelemetryProjector"/>:
///
/// AT-01 — Projector emits SchemaVersion = "0.5.0".
/// AT-02 — Two rooms produce a rooms[] array of length 2 with correct fields.
/// AT-03 — Three light sources produce a lightSources[] array of length 3 with correct fields.
/// AT-04 — One light aperture produces a lightApertures[] array of length 1 with correct fields.
/// AT-05 — Noon sun state projects correct azimuth, elevation, and DayPhase.
/// AT-06 — Midnight sun state projects negative elevation and DayPhase.Night.
/// AT-07 — No rooms → rooms is null or absent.
/// AT-08 — Full spatial snapshot validates against the v0.4 world-state schema.
/// AT-09 — Two projections of the same snapshot produce byte-identical JSON.
/// AT-10 — Rooms, light sources, and apertures are emitted sorted by id ascending.
/// AT-11 — Chronicle entries from ChronicleService are projected to WorldStateDto.Chronicle.
/// AT-12 — All pre-existing projector tests still pass.
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
    public void AT01_SchemaVersion_Is040()
    {
        var dto = Capture(MakeSim());
        Assert.Equal("0.5.0", dto.SchemaVersion);
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

    // ── AT-02: Rooms populated ────────────────────────────────────────────────

    [Fact]
    public void AT02_TwoRoomEntities_ProjectsRoomsArrayOfLength2()
    {
        var em = new EntityManager();
        EntityTemplates.Room(em, "room-a", "Breakroom A",
            EngRoomCategory.Breakroom, EngBuildingFloor.First,
            new BoundsRect(0, 0, 10, 8),
            illumination: new RoomIllumination(75, 4000, "src-1"));
        EntityTemplates.Room(em, "room-b", "Office B",
            EngRoomCategory.Office, EngBuildingFloor.First,
            new BoundsRect(10, 0, 12, 8));

        var snap = MakeSim(humanCount: 0).Capture();
        var dto  = TelemetryProjector.Project(snap, em, FixedCapture, 0, 42, "test");

        Assert.NotNull(dto.Rooms);
        Assert.Equal(2, dto.Rooms.Count);

        var breakroom = dto.Rooms.First(r => r.Id == "room-a");
        Assert.Equal("Breakroom A",          breakroom.Name);
        Assert.Equal(WcRoomCategory.Breakroom, breakroom.Category);
        Assert.Equal(WcBuildingFloor.First,    breakroom.Floor);
        Assert.Equal(0,  breakroom.BoundsRect.X);
        Assert.Equal(0,  breakroom.BoundsRect.Y);
        Assert.Equal(10, breakroom.BoundsRect.Width);
        Assert.Equal(8,  breakroom.BoundsRect.Height);
        Assert.Equal(75,     breakroom.Illumination.AmbientLevel);
        Assert.Equal(4000,   breakroom.Illumination.ColorTemperatureK);
        Assert.Equal("src-1", breakroom.Illumination.DominantSourceId);

        var office = dto.Rooms.First(r => r.Id == "room-b");
        Assert.Equal("Office B",          office.Name);
        Assert.Equal(WcRoomCategory.Office, office.Category);
    }

    // ── AT-03: Light sources populated ───────────────────────────────────────

    [Fact]
    public void AT03_ThreeLightSourceEntities_ProjectsLightSourcesArrayOfLength3()
    {
        var em = new EntityManager();
        EntityTemplates.LightSource(em, "ls-1",
            EngLightKind.OverheadFluorescent, EngLightState.On,
            intensity: 80, colorTemperatureK: 4000,
            tileX: 5, tileY: 3, roomId: "room-a");
        EntityTemplates.LightSource(em, "ls-2",
            EngLightKind.DeskLamp, EngLightState.Flickering,
            intensity: 40, colorTemperatureK: 3000,
            tileX: 2, tileY: 7, roomId: "room-a");
        EntityTemplates.LightSource(em, "ls-3",
            EngLightKind.ServerLed, EngLightState.Off,
            intensity: 0, colorTemperatureK: 6500,
            tileX: 9, tileY: 1, roomId: "room-b");

        var snap = MakeSim(humanCount: 0).Capture();
        var dto  = TelemetryProjector.Project(snap, em, FixedCapture, 0, 42, "test");

        Assert.NotNull(dto.LightSources);
        Assert.Equal(3, dto.LightSources.Count);

        var ls1 = dto.LightSources.First(s => s.Id == "ls-1");
        Assert.Equal(WcLightKind.OverheadFluorescent, ls1.Kind);
        Assert.Equal(WcLightState.On,                 ls1.State);
        Assert.Equal(80,   ls1.Intensity);
        Assert.Equal(4000, ls1.ColorTemperatureK);
        Assert.Equal(5,    ls1.Position.X);
        Assert.Equal(3,    ls1.Position.Y);
        Assert.Equal("room-a", ls1.RoomId);

        var ls2 = dto.LightSources.First(s => s.Id == "ls-2");
        Assert.Equal(WcLightKind.DeskLamp,    ls2.Kind);
        Assert.Equal(WcLightState.Flickering, ls2.State);
        Assert.Equal(3000, ls2.ColorTemperatureK);

        var ls3 = dto.LightSources.First(s => s.Id == "ls-3");
        Assert.Equal(WcLightState.Off, ls3.State);
    }

    // ── AT-04: Light apertures populated ─────────────────────────────────────

    [Fact]
    public void AT04_OneLightApertureEntity_ProjectsLightAperturesArrayOfLength1()
    {
        var em = new EntityManager();
        EntityTemplates.LightAperture(em, "ap-1",
            tileX: 4, tileY: 0, roomId: "room-a",
            facing: EngApertureFacing.South, areaSqTiles: 3.5);

        var snap = MakeSim(humanCount: 0).Capture();
        var dto  = TelemetryProjector.Project(snap, em, FixedCapture, 0, 42, "test");

        Assert.NotNull(dto.LightApertures);
        Assert.Single(dto.LightApertures);

        var ap = dto.LightApertures[0];
        Assert.Equal("ap-1",             ap.Id);
        Assert.Equal(4,                  ap.Position.X);
        Assert.Equal(0,                  ap.Position.Y);
        Assert.Equal("room-a",           ap.RoomId);
        Assert.Equal(WcApertureFacing.South, ap.Facing);
        Assert.Equal(3.5,                ap.AreaSqTiles, precision: 10);
    }

    // ── AT-05: Noon sun state ─────────────────────────────────────────────────

    [Fact]
    public void AT05_NoonSunState_ProjectsCorrectAzimuthElevationDayPhase()
    {
        var sunService = new SunStateService();
        sunService.UpdateSunState(new SunStateRecord(
            AzimuthDeg:   180.0,
            ElevationDeg:  89.0,
            DayPhase:      EngDayPhase.Afternoon));

        var snap = MakeSim(humanCount: 0).Capture();
        var dto  = TelemetryProjector.Project(
            snap, null, FixedCapture, 0, 42, "test",
            sunStateService: sunService);

        Assert.NotNull(dto.Clock.Sun);
        Assert.Equal(180.0, dto.Clock.Sun.AzimuthDeg,   precision: 6);
        Assert.True(dto.Clock.Sun.ElevationDeg > 0,
            $"Expected positive elevation at noon; got {dto.Clock.Sun.ElevationDeg}");
        Assert.Equal(WcDayPhase.Afternoon, dto.Clock.Sun.DayPhase);
    }

    // ── AT-06: Midnight sun state ─────────────────────────────────────────────

    [Fact]
    public void AT06_MidnightSunState_ProjectsNegativeElevationAndNightPhase()
    {
        var sunService = new SunStateService();
        sunService.UpdateSunState(new SunStateRecord(
            AzimuthDeg:    0.0,
            ElevationDeg: -30.0,
            DayPhase:      EngDayPhase.Night));

        var snap = MakeSim(humanCount: 0).Capture();
        var dto  = TelemetryProjector.Project(
            snap, null, FixedCapture, 0, 42, "test",
            sunStateService: sunService);

        Assert.NotNull(dto.Clock.Sun);
        Assert.True(dto.Clock.Sun.ElevationDeg < 0,
            $"Expected negative elevation at midnight; got {dto.Clock.Sun.ElevationDeg}");
        Assert.Equal(WcDayPhase.Night, dto.Clock.Sun.DayPhase);
    }

    // ── AT-07: No rooms → rooms is null ──────────────────────────────────────

    [Fact]
    public void AT07_NoRoomEntities_RoomsIsNull()
    {
        var em   = new EntityManager();
        var snap = MakeSim(humanCount: 0).Capture();
        var dto  = TelemetryProjector.Project(snap, em, FixedCapture, 0, 42, "test");

        Assert.Null(dto.Rooms);
    }

    // ── AT-08: Full spatial snapshot validates against v0.3 schema ────────────

    [Fact]
    public void AT08_FullSpatialSnapshot_ValidatesAgainstSchema()
    {
        var em = new EntityManager();
        EntityTemplates.Room(em, "room-x", "CubicleGrid",
            EngRoomCategory.CubicleGrid, EngBuildingFloor.First,
            new BoundsRect(0, 0, 20, 15),
            illumination: new RoomIllumination(60, 4200, null));
        EntityTemplates.LightSource(em, "ls-x",
            EngLightKind.OverheadFluorescent, EngLightState.On,
            intensity: 70, colorTemperatureK: 4200,
            tileX: 10, tileY: 7, roomId: "room-x");
        EntityTemplates.LightAperture(em, "ap-x",
            tileX: 0, tileY: 7, roomId: "room-x",
            facing: EngApertureFacing.West, areaSqTiles: 2.0);

        var sunService = new SunStateService();
        sunService.UpdateSunState(new SunStateRecord(
            AzimuthDeg:   180.0,
            ElevationDeg:  60.0,
            DayPhase:      EngDayPhase.Afternoon));

        var snap   = MakeSim(humanCount: 0).Capture();
        var dto    = TelemetryProjector.Project(snap, em, FixedCapture, 0, 42, "test", sunService);
        var json   = TelemetrySerializer.SerializeSnapshot(dto);
        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.True(result.IsValid,
            $"Schema validation failed: {string.Join("; ", result.Errors)}\n\nJSON:\n{json}");
    }

    // ── AT-09: Determinism with spatial state ─────────────────────────────────

    [Fact]
    public void AT09_TwoProjectionsOfSameSnapshot_ProduceBytIdenticalJson()
    {
        var em = new EntityManager();
        EntityTemplates.Room(em, "room-det", "Det Room",
            EngRoomCategory.Office, EngBuildingFloor.First,
            new BoundsRect(0, 0, 10, 10));
        EntityTemplates.LightSource(em, "ls-det",
            EngLightKind.DeskLamp, EngLightState.On,
            intensity: 50, colorTemperatureK: 3500,
            tileX: 5, tileY: 5, roomId: "room-det");

        var sunService = new SunStateService();
        sunService.UpdateSunState(new SunStateRecord(90.0, 45.0, EngDayPhase.MidMorning));

        var snap  = MakeSim(humanCount: 0).Capture();

        var json1 = TelemetrySerializer.SerializeSnapshot(
            TelemetryProjector.Project(snap, em, FixedCapture, 3L, 7, "v3", sunService));
        var json2 = TelemetrySerializer.SerializeSnapshot(
            TelemetryProjector.Project(snap, em, FixedCapture, 3L, 7, "v3", sunService));

        Assert.Equal(json1, json2);
    }

    // ── AT-10: Sorted by id ascending ─────────────────────────────────────────

    [Fact]
    public void AT10_RoomsLightSourcesAndApertures_SortedByIdAscending()
    {
        var em = new EntityManager();

        // Rooms — created in order; EntityManager assigns counter-based Guids so
        // ascending entity-Id sort matches creation order.
        EntityTemplates.Room(em, "room-sort-a", "Room A",
            EngRoomCategory.Hallway, EngBuildingFloor.First, new BoundsRect(0, 0, 5, 5));
        EntityTemplates.Room(em, "room-sort-b", "Room B",
            EngRoomCategory.Hallway, EngBuildingFloor.First, new BoundsRect(5, 0, 5, 5));

        EntityTemplates.LightSource(em, "ls-sort-a",
            EngLightKind.DeskLamp, EngLightState.On, 50, 3000, 1, 1, "room-sort-a");
        EntityTemplates.LightSource(em, "ls-sort-b",
            EngLightKind.DeskLamp, EngLightState.On, 50, 3000, 2, 2, "room-sort-b");

        EntityTemplates.LightAperture(em, "ap-sort-a",
            0, 2, "room-sort-a", EngApertureFacing.North, 1.0);
        EntityTemplates.LightAperture(em, "ap-sort-b",
            5, 2, "room-sort-b", EngApertureFacing.North, 1.0);

        var snap = MakeSim(humanCount: 0).Capture();
        var dto  = TelemetryProjector.Project(snap, em, FixedCapture, 0, 42, "test");

        Assert.NotNull(dto.Rooms);
        var roomIds = dto.Rooms.Select(r => r.Id).ToList();
        Assert.Equal(roomIds.OrderBy(x => x, StringComparer.Ordinal).ToList(), roomIds);

        Assert.NotNull(dto.LightSources);
        var lsIds = dto.LightSources.Select(s => s.Id).ToList();
        Assert.Equal(lsIds.OrderBy(x => x, StringComparer.Ordinal).ToList(), lsIds);

        Assert.NotNull(dto.LightApertures);
        var apIds = dto.LightApertures.Select(a => a.Id).ToList();
        Assert.Equal(apIds.OrderBy(x => x, StringComparer.Ordinal).ToList(), apIds);
    }

    // ── AT-11: Chronicle projection ───────────────────────────────────────────

    [Fact]
    public void AT11_ChronicleService_WithEntries_ProjectsToChronicleArray()
    {
        var svc = new ChronicleService();
        svc.Append(new ChronicleEntry(
            Id:                       "aaaa0001-0000-0000-0000-000000000000",
            Kind:                     APIFramework.Systems.Chronicle.ChronicleEventKind.SpilledSomething,
            Tick:                     10L,
            ParticipantIds:           new List<int> { 1, 2 },
            Location:                 "breakroom",
            Description:              "Coffee spilled on the table.",
            Persistent:               true,
            PhysicalManifestEntityId: null));
        svc.Append(new ChronicleEntry(
            Id:                       "aaaa0002-0000-0000-0000-000000000000",
            Kind:                     APIFramework.Systems.Chronicle.ChronicleEventKind.PublicArgument,
            Tick:                     20L,
            ParticipantIds:           new List<int>(),
            Location:                 string.Empty,
            Description:              "Heated words in the hallway.",
            Persistent:               true,
            PhysicalManifestEntityId: null));

        var snap = MakeSim(humanCount: 0).Capture();
        var dto  = TelemetryProjector.Project(
            snap, null, FixedCapture, 25L, 42, "test", chronicleService: svc);

        Assert.NotNull(dto.Chronicle);
        Assert.Equal(2, dto.Chronicle!.Count);

        // Sorted by tick ascending
        Assert.Equal(10L, dto.Chronicle[0].Tick);
        Assert.Equal(20L, dto.Chronicle[1].Tick);

        Assert.Equal(Warden.Contracts.Telemetry.ChronicleEventKind.SpilledSomething,
            dto.Chronicle[0].Kind);
        Assert.Equal("breakroom", dto.Chronicle[0].Location);
        Assert.Equal("Coffee spilled on the table.", dto.Chronicle[0].Description);
        Assert.True(dto.Chronicle[0].Persistent);

        // Participants are projected as GUID strings
        Assert.Equal(2, dto.Chronicle[0].Participants.Count);
    }

    [Fact]
    public void AT11_ChronicleService_Empty_ProducesNullChronicle()
    {
        var svc  = new ChronicleService();  // no entries
        var snap = MakeSim(humanCount: 0).Capture();
        var dto  = TelemetryProjector.Project(
            snap, null, FixedCapture, 0L, 42, "test", chronicleService: svc);

        Assert.Null(dto.Chronicle);
    }

    [Fact]
    public void AT11_NoChronicleService_ChronicleIsNull()
    {
        var snap = MakeSim(humanCount: 0).Capture();
        var dto  = TelemetryProjector.Project(
            snap, null, FixedCapture, 0L, 42, "test");

        Assert.Null(dto.Chronicle);
    }

    [Fact]
    public void AT11_ChronicleProjection_ValidatesAgainstSchema()
    {
        var svc = new ChronicleService();
        svc.Append(new ChronicleEntry(
            Id:                       "aaaa0003-0000-0000-0000-000000000000",
            Kind:                     APIFramework.Systems.Chronicle.ChronicleEventKind.PublicArgument,
            Tick:                     5L,
            ParticipantIds:           new List<int>(),
            Location:                 "office",
            Description:              "A short description.",
            Persistent:               true,
            PhysicalManifestEntityId: null));

        var snap   = MakeSim(humanCount: 0).Capture();
        var dto    = TelemetryProjector.Project(
            snap, null, FixedCapture, 5L, 42, "test", chronicleService: svc);
        var json   = TelemetrySerializer.SerializeSnapshot(dto);
        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.True(result.IsValid,
            $"Schema validation failed: {string.Join("; ", result.Errors)}\n\nJSON:\n{json}");
    }

    // ── AT-12: Existing tests (unchanged below this line) ────────────────────

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
