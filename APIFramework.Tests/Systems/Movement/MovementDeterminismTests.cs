using System;
using System.Collections.Generic;
using System.Text;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Movement;

/// <summary>
/// AT-15: Two runs with the same seed over 5000 ticks produce byte-identical
/// position and facing trajectories.
/// </summary>
public class MovementDeterminismTests
{
    [Fact]
    public void TwoRuns_SameSeed_ProduceIdenticalTrajectories()
    {
        const int seed  = 77777;
        const int ticks = 5000;
        const int npcs  = 6;

        var trace1 = RunTrace(seed, ticks, npcs);
        var trace2 = RunTrace(seed, ticks, npcs);

        Assert.Equal(trace1.Length, trace2.Length);
        Assert.Equal(trace1, trace2);
    }

    [Fact]
    public void TwoRuns_DifferentSeeds_ProduceDifferentTrajectories()
    {
        const int ticks = 5000;
        const int npcs  = 6;

        var trace1 = RunTrace(seed: 1, ticks, npcs);
        var trace2 = RunTrace(seed: 2, ticks, npcs);

        Assert.NotEqual(trace1, trace2);
    }

    private static string RunTrace(int seed, int ticks, int npcCount)
    {
        var rng = new SeededRandom(seed);
        var em  = new EntityManager();
        var idx = new GridSpatialIndex(4, 64, 64);

        var structBus = new StructuralChangeBus();
        var syncSys = new SpatialIndexSyncSystem(idx, structBus);
        em.EntityDestroyed += syncSys.OnEntityDestroyed;

        var membership = new EntityRoomMembership();
        var bus        = new ProximityEventBus();
        var cfg        = new MovementConfig();
        var movCfg     = new MovementConfig();

        var roomSys  = new RoomMembershipSystem(membership, bus, structBus);
        var proxSys  = new ProximityEventSystem(idx, bus, membership);
        var cache = new PathfindingCache(512);
        var pathSvc  = new PathfindingService(em, 64, 64, cfg, cache, structBus);
        var trigSys  = new PathfindingTriggerSystem(pathSvc);
        var speedSys = new MovementSpeedModifierSystem(movCfg);
        var stepSys  = new StepAsideSystem(idx, membership, movCfg);
        var moveSys  = new MovementSystem(rng) { WorldMinX = 0, WorldMaxX = 63, WorldMinZ = 0, WorldMaxZ = 63 };
        var faceSys  = new FacingSystem(bus);
        var idleSys  = new IdleMovementSystem(rng, movCfg);

        // Spawn NPCs
        var npcs = new Entity[npcCount];
        for (int i = 0; i < npcCount; i++)
        {
            var e = em.CreateEntity();
            e.Add(new PositionComponent { X = rng.NextFloat() * 60f, Y = 0f, Z = rng.NextFloat() * 60f });
            e.Add(new MovementComponent { Speed = 0.1f, ArrivalDistance = 0.5f, SpeedModifier = 1.0f });
            e.Add(new FacingComponent { DirectionDeg = 0f, Source = FacingSource.Idle });
            e.Add(new HandednessComponent { Side = HandednessSide.RightSidePass });
            e.Add(new NpcTag());
            e.Add(ProximityComponent.Default);
            npcs[i] = e;

            int tx = (int)MathF.Round(e.Get<PositionComponent>().X);
            int ty = (int)MathF.Round(e.Get<PositionComponent>().Z);
            idx.Register(e, tx, ty);
        }

        var sb = new StringBuilder();

        for (int t = 0; t < ticks; t++)
        {
            syncSys.Update(em, 1f);
            roomSys.Update(em, 1f);
            proxSys.Update(em, 1f);
            trigSys.Update(em, 1f);
            speedSys.Update(em, 1f);
            stepSys.Update(em, 1f);
            moveSys.Update(em, 1f);
            faceSys.Update(em, 1f);
            idleSys.Update(em, 1f);

            if (t % 100 == 0)
            {
                foreach (var npc in npcs)
                {
                    var pos = npc.Get<PositionComponent>();
                    sb.Append($"t{t}:{pos.X:F4},{pos.Z:F4}");

                    if (npc.Has<FacingComponent>())
                    {
                        var f = npc.Get<FacingComponent>();
                        sb.Append($",f{f.DirectionDeg:F2}");
                    }
                    sb.Append(';');
                }
            }
        }

        return sb.ToString();
    }
}
