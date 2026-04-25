using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Coupling;
using APIFramework.Systems.Lighting;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Coupling;

/// <summary>
/// AT-12: Two runs with the same seed produce byte-identical drive trajectories over 5000 ticks
/// under varied lighting conditions (flickering source, desk lamp, dim hallway, aperture beam,
/// pitch dark). Verifies the determinism contract for LightingToDriveCouplingSystem.
/// </summary>
public class CouplingDeterminismTests
{
    [Fact]
    public void TwoRunsSameSeed_ProduceIdenticalDriveTrajectories_Over5000Ticks()
    {
        const int ticks = 5000;
        const int seed  = 31337;

        var t1 = RunTrajectory(ticks, seed);
        var t2 = RunTrajectory(ticks, seed);

        Assert.Equal(t1.Count, t2.Count);
        for (int i = 0; i < t1.Count; i++)
        {
            Assert.True(t1[i] == t2[i],
                $"Tick {i}: trajectory diverged ({t1[i]} vs {t2[i]})");
        }
    }

    private static List<(int irritation, int loneliness, int belonging, int suspicion)> RunTrajectory(
        int ticks, int seed)
    {
        // World: five rooms with varied lighting + five NPCs, one per room.
        var em          = new EntityManager();
        var membership  = new EntityRoomMembership();
        var sunSvc      = new SunStateService();
        var clock       = new SimulationClock();
        var rng         = new SeededRandom(seed);

        // Deterministic sun state: afternoon phase
        sunSvc.UpdateSunState(new SunStateRecord(
            AzimuthDeg: 180, ElevationDeg: 60, DayPhase: DayPhase.Afternoon));

        var table = BuildTable();
        var acc   = new SocialDriveAccumulator();
        var apertureBeamSys = new ApertureBeamSystem(sunSvc, clock);
        var sys   = new LightingToDriveCouplingSystem(table, acc, membership, apertureBeamSys, sunSvc);

        // Room A: flickering source
        var flickerSourceId = Guid.NewGuid().ToString();
        var roomA = CreateRoom(em, Guid.NewGuid().ToString(), RoomCategory.Hallway,
            new RoomIllumination(40, 4000, flickerSourceId));
        CreateLightSource(em, flickerSourceId, LightKind.OverheadFluorescent, LightState.Flickering, roomA);
        var npcA = CreateNpc(em, membership, roomA);

        // Room B: desk lamp office
        var lampSourceId = Guid.NewGuid().ToString();
        var roomB = CreateRoom(em, Guid.NewGuid().ToString(), RoomCategory.Office,
            new RoomIllumination(60, 3000, lampSourceId));
        CreateLightSource(em, lampSourceId, LightKind.DeskLamp, LightState.On, roomB);
        var npcB = CreateNpc(em, membership, roomB);

        // Room C: dim hallway (no dominant source, ambient low)
        var roomCId = Guid.NewGuid().ToString();
        var roomC   = CreateRoom(em, roomCId, RoomCategory.Hallway,
            new RoomIllumination(10, 2700, null));
        var npcC = CreateNpc(em, membership, roomC);
        // Aperture in room C so AT-05 path can be exercised (sun is up, south-facing window)
        var aperture = em.CreateEntity();
        aperture.Add(new LightApertureTag());
        aperture.Add(new LightApertureComponent
        {
            Id = Guid.NewGuid().ToString(), TileX = 5, TileY = 0,
            RoomId = roomCId, Facing = ApertureFacing.South, AreaSqTiles = 4.0
        });

        // Room D: pitch dark
        var roomD = CreateRoom(em, Guid.NewGuid().ToString(), RoomCategory.CubicleGrid,
            new RoomIllumination(3, 0, null));
        var npcD = CreateNpc(em, membership, roomD);

        // Room E: server LED
        var ledSourceId = Guid.NewGuid().ToString();
        var roomE = CreateRoom(em, Guid.NewGuid().ToString(), RoomCategory.ItCloset,
            new RoomIllumination(35, 6500, ledSourceId));
        CreateLightSource(em, ledSourceId, LightKind.ServerLed, LightState.On, roomE);
        var npcE = CreateNpc(em, membership, roomE);

        var trajectory = new List<(int, int, int, int)>(ticks);
        for (int tick = 0; tick < ticks; tick++)
        {
            // Update sun every 100 ticks to exercise phase changes deterministically
            if (tick % 100 == 0)
            {
                var phases = new[] { DayPhase.MidMorning, DayPhase.Afternoon, DayPhase.Evening,
                                     DayPhase.Dusk, DayPhase.Night };
                var ph = phases[(tick / 100) % phases.Length];
                double elev = ph is DayPhase.Night or DayPhase.Dusk ? -5.0 : 60.0;
                sunSvc.UpdateSunState(new SunStateRecord(180, elev, ph));
            }

            apertureBeamSys.Update(em, 1f);
            sys.Update(em, 1f);

            // Sample drive values from all five NPCs
            var da = npcA.Get<SocialDrivesComponent>();
            var db = npcB.Get<SocialDrivesComponent>();
            var dc = npcC.Get<SocialDrivesComponent>();
            var dd = npcD.Get<SocialDrivesComponent>();
            var de = npcE.Get<SocialDrivesComponent>();

            trajectory.Add((
                da.Irritation.Current + db.Belonging.Current + dc.Suspicion.Current
                    + dd.Loneliness.Current + de.Irritation.Current,
                da.Loneliness.Current + db.Affection.Current + dc.Irritation.Current
                    + dd.Belonging.Current + de.Belonging.Current,
                da.Belonging.Current + db.Belonging.Current + dc.Belonging.Current
                    + dd.Belonging.Current + de.Belonging.Current,
                da.Suspicion.Current + db.Suspicion.Current + dc.Suspicion.Current
                    + dd.Suspicion.Current + de.Suspicion.Current
            ));
        }

        return trajectory;
    }

    private static LightingDriveCouplingTable BuildTable() =>
        new(new List<LightingCouplingEntry>
        {
            new()
            {
                Condition     = new CouplingCondition { DominantSourceState = "flickering" },
                DeltasPerTick = new() { ["irritation"] = 0.08f, ["loneliness"] = 0.02f }
            },
            new()
            {
                Condition = new CouplingCondition
                {
                    RoomCategoryAny = new() { "cubicleGrid", "office" },
                    DominantSourceKind = "deskLamp", AmbientLevelMin = 30
                },
                DeltasPerTick = new() { ["belonging"] = 0.05f, ["affection"] = 0.04f }
            },
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
            new()
            {
                Condition     = new CouplingCondition { ApertureBeamPresent = true },
                DeltasPerTick = new() { ["loneliness"] = -0.05f, ["belonging"] = 0.03f }
            },
            new()
            {
                Condition     = new CouplingCondition { AmbientLevelMax = 5 },
                DeltasPerTick = new() { ["belonging"] = -0.03f, ["loneliness"] = 0.06f }
            },
            new()
            {
                Condition = new CouplingCondition
                {
                    RoomCategoryAny    = new() { "itCloset" },
                    DominantSourceKind = "serverLed"
                },
                DeltasPerTick = new() { ["irritation"] = 0.03f }
            },
        });

    private static Entity CreateRoom(EntityManager em, string roomId,
        RoomCategory category, RoomIllumination illum)
    {
        var room = em.CreateEntity();
        room.Add(new RoomTag());
        room.Add(new RoomComponent
        {
            Id = roomId, Name = category.ToString(), Category = category,
            Floor = BuildingFloor.First, Bounds = new BoundsRect(0, 0, 10, 10),
            Illumination = illum
        });
        return room;
    }

    private static void CreateLightSource(EntityManager em, string sourceId,
        LightKind kind, LightState state, Entity room)
    {
        var src = em.CreateEntity();
        src.Add(new LightSourceTag());
        src.Add(new LightSourceComponent
        {
            Id = sourceId, Kind = kind, State = state, Intensity = 70,
            ColorTemperatureK = 4000, TileX = 5, TileY = 5,
            RoomId = room.Get<RoomComponent>().Id
        });
    }

    private static Entity CreateNpc(EntityManager em, EntityRoomMembership membership, Entity room)
    {
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialDrivesComponent
        {
            Belonging  = new DriveValue { Current = 50, Baseline = 50 },
            Loneliness = new DriveValue { Current = 50, Baseline = 50 },
            Irritation = new DriveValue { Current = 50, Baseline = 50 },
            Suspicion  = new DriveValue { Current = 50, Baseline = 50 },
        });
        membership.SetRoom(npc, room);
        return npc;
    }
}
