using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Coupling;
using APIFramework.Systems.Lighting;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Coupling;

/// <summary>
/// Unit tests for LightingToDriveCouplingSystem (AT-01 through AT-11).
/// Each test constructs a minimal world with precisely the entities needed.
/// </summary>
public class LightingToDriveCouplingSystemTests
{
    // -- Helpers ----------------------------------------------------------------

    private static LightingDriveCouplingTable DefaultTable() =>
        new(new List<LightingCouplingEntry>
        {
            // Entry 0: flickering dominant source → irritation + loneliness
            new()
            {
                Condition     = new CouplingCondition { DominantSourceState = "flickering" },
                DeltasPerTick = new() { ["irritation"] = 0.08f, ["loneliness"] = 0.02f }
            },
            // Entry 1: desk lamp in office, ambient ≥ 30 → belonging + affection
            new()
            {
                Condition = new CouplingCondition
                {
                    RoomCategoryAny   = new() { "cubicleGrid", "office" },
                    DominantSourceKind = "deskLamp",
                    AmbientLevelMin   = 30
                },
                DeltasPerTick = new() { ["belonging"] = 0.05f, ["affection"] = 0.04f }
            },
            // Entry 2: dim hallway evening/dusk/night → suspicion + irritation
            new()
            {
                Condition = new CouplingCondition
                {
                    RoomCategoryAny = new() { "hallway", "stairwell" },
                    AmbientLevelMax = 20,
                    DayPhaseAny     = new() { "evening", "dusk", "night" }
                },
                DeltasPerTick = new() { ["suspicion"] = 0.10f, ["irritation"] = 0.04f }
            },
            // Entry 3: aperture beam present → loneliness recovery + belonging
            new()
            {
                Condition     = new CouplingCondition { ApertureBeamPresent = true },
                DeltasPerTick = new() { ["loneliness"] = -0.05f, ["belonging"] = 0.03f }
            },
            // Entry 4: pitch dark (ambient ≤ 5) → belonging decay + loneliness rise
            new()
            {
                Condition     = new CouplingCondition { AmbientLevelMax = 5 },
                DeltasPerTick = new() { ["belonging"] = -0.03f, ["loneliness"] = 0.06f }
            },
        });

    /// <summary>Creates a room entity with the given category and illumination snapshot.</summary>
    private static Entity CreateRoom(EntityManager em, RoomCategory category,
        RoomIllumination illumination, string? roomId = null)
    {
        var id = roomId ?? Guid.NewGuid().ToString();
        var room = em.CreateEntity();
        room.Add(new RoomTag());
        room.Add(new RoomComponent
        {
            Id          = id,
            Name        = category.ToString(),
            Category    = category,
            Floor       = BuildingFloor.First,
            Bounds      = new BoundsRect(0, 0, 10, 10),
            Illumination = illumination
        });
        return room;
    }

    /// <summary>Creates an NPC entity with zeroed drives and places it in the given room.</summary>
    private static Entity CreateNpc(EntityManager em, EntityRoomMembership membership, Entity roomEntity)
    {
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialDrivesComponent());
        membership.SetRoom(npc, roomEntity);
        return npc;
    }

    private static LightingToDriveCouplingSystem BuildSystem(
        LightingDriveCouplingTable? table        = null,
        SocialDriveAccumulator?     accumulator  = null,
        EntityRoomMembership?       membership   = null,
        ApertureBeamSystem?         apertureBeam = null,
        SunStateService?            sunService   = null)
    {
        var sunSvc = sunService ?? new SunStateService();
        var clock  = new SimulationClock();
        return new LightingToDriveCouplingSystem(
            table        ?? DefaultTable(),
            accumulator  ?? new SocialDriveAccumulator(),
            membership   ?? new EntityRoomMembership(),
            apertureBeam ?? new ApertureBeamSystem(sunSvc, clock),
            sunSvc);
    }

    // -- AT-01 -----------------------------------------------------------------

    [Fact]
    public void AT01_EmptyWorld_RunsWithoutError()
    {
        var em  = new EntityManager();
        var sys = BuildSystem();
        var ex  = Record.Exception(() => sys.Update(em, 1f));
        Assert.Null(ex);
    }

    // -- AT-02 -----------------------------------------------------------------

    [Fact]
    public void AT02_FlickeringSource_InHallway_IrritationIncrements4OrMoreOver60Ticks()
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();

        // Room illuminated by a flickering source
        var sourceId   = Guid.NewGuid().ToString();
        var illum      = new RoomIllumination(AmbientLevel: 40, ColorTemperatureK: 4000, DominantSourceId: sourceId);
        var room       = CreateRoom(em, RoomCategory.Hallway, illum, Guid.NewGuid().ToString());

        // Flickering light source entity
        var src = em.CreateEntity();
        src.Add(new LightSourceTag());
        src.Add(new LightSourceComponent
        {
            Id = sourceId, Kind = LightKind.OverheadFluorescent, State = LightState.Flickering,
            Intensity = 60, ColorTemperatureK = 4000, TileX = 5, TileY = 5, RoomId = room.Get<RoomComponent>().Id
        });

        var npc = CreateNpc(em, membership, room);
        var acc = new SocialDriveAccumulator();
        var sys = BuildSystem(accumulator: acc, membership: membership);

        for (int i = 0; i < 60; i++) sys.Update(em, 1f);

        var drives = npc.Get<SocialDrivesComponent>();
        Assert.True(drives.Irritation.Current >= 4,
            $"Expected irritation ≥ 4 after 60 ticks of 0.08/tick, got {drives.Irritation.Current}");
        Assert.True(drives.Loneliness.Current >= 1,
            $"Expected loneliness ≥ 1 after 60 ticks of 0.02/tick, got {drives.Loneliness.Current}");
    }

    // -- AT-03 -----------------------------------------------------------------

    [Fact]
    public void AT03_WarmDeskLamp_InOffice_ProducesBelongingAndAffectionDelta()
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();

        var sourceId = Guid.NewGuid().ToString();
        var illum    = new RoomIllumination(AmbientLevel: 60, ColorTemperatureK: 3000, DominantSourceId: sourceId);
        var room     = CreateRoom(em, RoomCategory.Office, illum);

        var src = em.CreateEntity();
        src.Add(new LightSourceTag());
        src.Add(new LightSourceComponent
        {
            Id = sourceId, Kind = LightKind.DeskLamp, State = LightState.On,
            Intensity = 60, ColorTemperatureK = 3000, TileX = 3, TileY = 3, RoomId = room.Get<RoomComponent>().Id
        });

        var npc = CreateNpc(em, membership, room);
        var acc = new SocialDriveAccumulator();
        var sys = BuildSystem(accumulator: acc, membership: membership);

        // 0.05/tick belonging → flush after 20 ticks; 0.04/tick affection → flush after 25 ticks
        for (int i = 0; i < 30; i++) sys.Update(em, 1f);

        var drives = npc.Get<SocialDrivesComponent>();
        Assert.True(drives.Belonging.Current >= 1,
            $"Expected belonging ≥ 1 after 30 ticks of 0.05/tick, got {drives.Belonging.Current}");
        Assert.True(drives.Affection.Current >= 1,
            $"Expected affection ≥ 1 after 30 ticks of 0.04/tick, got {drives.Affection.Current}");
    }

    // -- AT-04 -----------------------------------------------------------------

    [Fact]
    public void AT04_DimHallway_Evening_ProducesSuspicionAndIrritationDelta()
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();
        var sunSvc     = new SunStateService();

        // Set sun to evening phase
        sunSvc.UpdateSunState(new SunStateRecord(AzimuthDeg: 270, ElevationDeg: 5, DayPhase: DayPhase.Evening));

        var illum = new RoomIllumination(AmbientLevel: 12, ColorTemperatureK: 2700, DominantSourceId: null);
        var room  = CreateRoom(em, RoomCategory.Hallway, illum);
        var npc   = CreateNpc(em, membership, room);
        var acc   = new SocialDriveAccumulator();
        var clock = new SimulationClock();
        var sys   = new LightingToDriveCouplingSystem(
            DefaultTable(), acc, membership,
            new ApertureBeamSystem(sunSvc, clock), sunSvc);

        // 0.10/tick suspicion → flush after 10 ticks; 0.04/tick irritation → flush after 25 ticks
        for (int i = 0; i < 25; i++) sys.Update(em, 1f);

        var drives = npc.Get<SocialDrivesComponent>();
        Assert.True(drives.Suspicion.Current >= 2,
            $"Expected suspicion ≥ 2 after 25 ticks, got {drives.Suspicion.Current}");
        Assert.True(drives.Irritation.Current >= 1,
            $"Expected irritation ≥ 1 after 25 ticks, got {drives.Irritation.Current}");
    }

    // -- AT-05 -----------------------------------------------------------------

    [Fact]
    public void AT05_SunBeamPresent_ProducesBelongingIncreaseAndLonelinessDecrease()
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();
        var sunSvc     = new SunStateService();
        var clock      = new SimulationClock();

        // Noon sun, high elevation — south-facing aperture will admit a beam
        sunSvc.UpdateSunState(new SunStateRecord(AzimuthDeg: 180, ElevationDeg: 90, DayPhase: DayPhase.MidMorning));

        var roomId = Guid.NewGuid().ToString();
        var illum  = new RoomIllumination(AmbientLevel: 50, ColorTemperatureK: 5500, DominantSourceId: null);
        var room   = CreateRoom(em, RoomCategory.Office, illum, roomId);

        // South-facing aperture in the same room
        var aperture = em.CreateEntity();
        aperture.Add(new LightApertureTag());
        aperture.Add(new LightApertureComponent
        {
            Id = Guid.NewGuid().ToString(), TileX = 5, TileY = 0,
            RoomId = roomId, Facing = ApertureFacing.South, AreaSqTiles = 4.0
        });

        var npc = CreateNpc(em, membership, room);
        npc.Get<SocialDrivesComponent>(); // ensure zeroed
        var drives0 = npc.Get<SocialDrivesComponent>();
        drives0.Loneliness.Current = 30; // give loneliness something to decrement
        npc.Add(drives0);

        var apertureBeamSys = new ApertureBeamSystem(sunSvc, clock);
        var acc = new SocialDriveAccumulator();
        var sys = new LightingToDriveCouplingSystem(DefaultTable(), acc, membership, apertureBeamSys, sunSvc);

        // -0.05/tick loneliness → flush after 20 ticks; +0.03/tick belonging → flush after 34 ticks
        for (int i = 0; i < 40; i++)
        {
            apertureBeamSys.Update(em, 1f);  // populate beam cache before coupling
            sys.Update(em, 1f);
        }

        var drives = npc.Get<SocialDrivesComponent>();
        Assert.True(drives.Loneliness.Current < 30,
            $"Expected loneliness < 30 after 40 ticks of -0.05/tick, got {drives.Loneliness.Current}");
        Assert.True(drives.Belonging.Current >= 1,
            $"Expected belonging ≥ 1 after 40 ticks of 0.03/tick, got {drives.Belonging.Current}");
    }

    // -- AT-06 -----------------------------------------------------------------

    [Fact]
    public void AT06_PitchDarkRoom_ProducesBelongingDecayAndLonelinessRise()
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();

        var illum = new RoomIllumination(AmbientLevel: 2, ColorTemperatureK: 0, DominantSourceId: null);
        var room  = CreateRoom(em, RoomCategory.CubicleGrid, illum);
        var npc   = CreateNpc(em, membership, room);

        var drives0 = npc.Get<SocialDrivesComponent>();
        drives0.Belonging.Current = 30;
        npc.Add(drives0);

        var acc = new SocialDriveAccumulator();
        var sys = BuildSystem(accumulator: acc, membership: membership);

        // -0.03/tick belonging → flush after ~34 ticks; +0.06/tick loneliness → flush after ~17 ticks
        for (int i = 0; i < 40; i++) sys.Update(em, 1f);

        var drives = npc.Get<SocialDrivesComponent>();
        Assert.True(drives.Belonging.Current < 30,
            $"Expected belonging < 30 after 40 ticks of -0.03/tick, got {drives.Belonging.Current}");
        Assert.True(drives.Loneliness.Current >= 2,
            $"Expected loneliness ≥ 2 after 40 ticks of 0.06/tick, got {drives.Loneliness.Current}");
    }

    // -- AT-07 -----------------------------------------------------------------

    [Fact]
    public void AT07_NpcWithNoRoom_ReceivesNoDelta()
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialDrivesComponent()); // membership.SetRoom NOT called → room is null

        var acc = new SocialDriveAccumulator();
        var sys = BuildSystem(accumulator: acc, membership: membership);

        for (int i = 0; i < 100; i++) sys.Update(em, 1f);

        var drives = npc.Get<SocialDrivesComponent>();
        Assert.Equal(0, drives.Irritation.Current);
        Assert.Equal(0, drives.Loneliness.Current);
        Assert.Equal(0, drives.Belonging.Current);
        Assert.Equal(0, drives.Suspicion.Current);
    }

    // -- AT-08 -----------------------------------------------------------------

    [Fact]
    public void AT08_FirstMatchWins_OnlyFirstMatchingEntryApplies()
    {
        // Custom table with two entries that would both match for dim rooms.
        // Entry 0: ambientLevelMax 50 → irritation += 5.0 (fast flush for test clarity)
        // Entry 1: ambientLevelMax 100 (matches any ambient) → loneliness += 5.0
        var table = new LightingDriveCouplingTable(new List<LightingCouplingEntry>
        {
            new()
            {
                Condition     = new CouplingCondition { AmbientLevelMax = 50 },
                DeltasPerTick = new() { ["irritation"] = 5.0f }
            },
            new()
            {
                Condition     = new CouplingCondition { AmbientLevelMax = 100 },
                DeltasPerTick = new() { ["loneliness"] = 5.0f }
            },
        });

        var em         = new EntityManager();
        var membership = new EntityRoomMembership();
        var illum      = new RoomIllumination(AmbientLevel: 25, ColorTemperatureK: 3000, DominantSourceId: null);
        var room       = CreateRoom(em, RoomCategory.Office, illum);
        var npc        = CreateNpc(em, membership, room);
        var acc        = new SocialDriveAccumulator();
        var sys        = BuildSystem(table: table, accumulator: acc, membership: membership);

        sys.Update(em, 1f);

        var drives = npc.Get<SocialDrivesComponent>();
        Assert.True(drives.Irritation.Current >= 5, $"Expected irritation ≥ 5, got {drives.Irritation.Current}");
        Assert.Equal(0, drives.Loneliness.Current); // second entry must NOT have applied
    }

    // -- AT-09 -----------------------------------------------------------------

    [Fact]
    public void AT09_SubOneDelta_AccumulatesAndProducesIntegerIncrementsAtExpectedRate()
    {
        // delta = 0.25/tick (exactly representable in binary)
        // Expected integer increments after N ticks = floor(0.25 × N)
        // Tick 4:  floor(1.0) = 1
        // Tick 8:  floor(2.0) = 2
        // Tick 100: floor(25.0) = 25
        var table = new LightingDriveCouplingTable(new List<LightingCouplingEntry>
        {
            new()
            {
                Condition     = new CouplingCondition(),  // matches everything
                DeltasPerTick = new() { ["irritation"] = 0.25f }
            }
        });

        var em         = new EntityManager();
        var membership = new EntityRoomMembership();
        var room       = CreateRoom(em, RoomCategory.Office,
            new RoomIllumination(AmbientLevel: 50, ColorTemperatureK: 4000, DominantSourceId: null));
        var npc = CreateNpc(em, membership, room);
        var acc = new SocialDriveAccumulator();
        var sys = BuildSystem(table: table, accumulator: acc, membership: membership);

        for (int i = 0; i < 100; i++) sys.Update(em, 1f);

        Assert.Equal(25, npc.Get<SocialDrivesComponent>().Irritation.Current);
    }

    // -- AT-10 -----------------------------------------------------------------

    [Fact]
    public void AT10_DriveCurrentClampsAt100_WithSustainedPositiveDeltas()
    {
        var table = new LightingDriveCouplingTable(new List<LightingCouplingEntry>
        {
            new()
            {
                Condition     = new CouplingCondition(),
                DeltasPerTick = new() { ["irritation"] = 10.0f }  // large delta
            }
        });

        var em         = new EntityManager();
        var membership = new EntityRoomMembership();
        var room       = CreateRoom(em, RoomCategory.Hallway,
            new RoomIllumination(AmbientLevel: 50, ColorTemperatureK: 4000, DominantSourceId: null));
        var npc = CreateNpc(em, membership, room);
        var acc = new SocialDriveAccumulator();
        var sys = BuildSystem(table: table, accumulator: acc, membership: membership);

        for (int i = 0; i < 20; i++) sys.Update(em, 1f);  // would push to 200 without clamping

        Assert.Equal(100, npc.Get<SocialDrivesComponent>().Irritation.Current);
    }

    // -- AT-11 -----------------------------------------------------------------

    [Fact]
    public void AT11_DriveCurrentClampsAt0_WithSustainedNegativeDeltas()
    {
        var table = new LightingDriveCouplingTable(new List<LightingCouplingEntry>
        {
            new()
            {
                Condition     = new CouplingCondition(),
                DeltasPerTick = new() { ["belonging"] = -10.0f }  // large negative delta
            }
        });

        var em         = new EntityManager();
        var membership = new EntityRoomMembership();
        var room       = CreateRoom(em, RoomCategory.Hallway,
            new RoomIllumination(AmbientLevel: 50, ColorTemperatureK: 4000, DominantSourceId: null));
        var npc = CreateNpc(em, membership, room);
        var acc = new SocialDriveAccumulator();
        var sys = BuildSystem(table: table, accumulator: acc, membership: membership);

        // Start belonging at 50 so it has room to fall
        var drives = npc.Get<SocialDrivesComponent>();
        drives.Belonging.Current = 50;
        npc.Add(drives);

        for (int i = 0; i < 20; i++) sys.Update(em, 1f);  // would push to -150 without clamping

        Assert.Equal(0, npc.Get<SocialDrivesComponent>().Belonging.Current);
    }
}
