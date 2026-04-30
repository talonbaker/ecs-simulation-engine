using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Determinism;

/// <summary>
/// AT-13: 5000-tick run with scripted IWorldMutationApi.MoveEntity calls at fixed ticks.
/// Two runs (same entity creation order, same mutations) must produce byte-identical
/// end-state (entity positions and topology version).
/// </summary>
public class LiveMutationDeterminismTests
{
    private sealed class World
    {
        public EntityManager     Em;
        public StructuralChangeBus Bus;
        public PathfindingCache  Cache;
        public PathfindingService Pathfinding;
        public WorldMutationApi  Api;
        public List<Entity>      Desks = new();

        public World()
        {
            Em          = new EntityManager();
            Bus         = new StructuralChangeBus();
            Cache       = new PathfindingCache(512);
            Pathfinding = new PathfindingService(Em, 64, 64, new MovementConfig(), Bus, Cache);
            Api         = new WorldMutationApi(Em, Bus);
            Bus.Subscribe(_ => Cache.Clear());

            // Spawn 5 desks (MutableTopologyTag entities) at fixed positions
            for (int i = 0; i < 5; i++)
            {
                var desk = Em.CreateEntity();
                desk.Add(default(StructuralTag));
                desk.Add(default(MutableTopologyTag));
                desk.Add(default(ObstacleTag));
                desk.Add(new PositionComponent { X = i * 5, Z = 5 });
                Desks.Add(desk);
            }
        }
    }

    private static void RunMutations(World w, int totalTicks)
    {
        // Scripted mutations at deterministic tick intervals
        for (int tick = 1; tick <= totalTicks; tick++)
        {
            if (tick % 100 == 0)
            {
                int deskIndex  = (tick / 100 - 1) % w.Desks.Count;
                int newX = (tick % 30) + 1;
                int newZ = (tick % 20) + 1;
                w.Api.MoveEntity(w.Desks[deskIndex].Id, newX, newZ);
            }

            if (tick % 250 == 0)
            {
                // Compute a path to exercise cache coherence
                w.Pathfinding.ComputePath(0, 0, 30, 30, seed: tick);
            }
        }
    }

    [Fact]
    public void TwoRuns_SameScriptedMutations_ProduceIdenticalEndState()
    {
        const int Ticks = 5000;

        var w1 = new World();
        var w2 = new World();

        RunMutations(w1, Ticks);
        RunMutations(w2, Ticks);

        // Topology versions must be identical
        Assert.Equal(w1.Bus.TopologyVersion, w2.Bus.TopologyVersion);

        // All desk positions must be identical
        for (int i = 0; i < w1.Desks.Count; i++)
        {
            var pos1 = w1.Desks[i].Get<PositionComponent>();
            var pos2 = w2.Desks[i].Get<PositionComponent>();
            Assert.Equal(pos1.X, pos2.X);
            Assert.Equal(pos1.Z, pos2.Z);
        }
    }
}
