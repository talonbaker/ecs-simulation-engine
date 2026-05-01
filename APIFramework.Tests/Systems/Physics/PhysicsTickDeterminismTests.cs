using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Audio;
using APIFramework.Systems.Physics;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Physics;

/// <summary>AT-11: 5000 ticks of deterministic throw events produce byte-identical state.</summary>
public class PhysicsTickDeterminismTests
{
    private static List<(float X, float Y, float Z)> RunScenario(int seed)
    {
        var em    = new EntityManager();
        var bus   = new StructuralChangeBus();
        var api   = new WorldMutationApi(em, bus);
        var sound = new SoundTriggerBus();
        var clock = new SimulationClock();
        var cfg   = new PhysicsConfig
        {
            GravityPerTick   = 1.5f,
            MinVelocity      = 0.05f,
            WallHitClampMargin = 0.01f
        };
        var col = new CollisionDetector(cfg, 100, 100);
        var sys = new PhysicsTickSystem(cfg, col, api, sound, clock);

        // Spawn 3 identical thrown objects
        for (int i = 0; i < 3; i++)
        {
            var e = em.CreateEntity();
            e.Add(new PositionComponent { X = 10f + i, Y = 5f, Z = 10f });
            e.Add(new MassComponent { MassKilograms = 1.2f });
            e.Add(new ThrownVelocityComponent { VelocityX = 3f, VelocityZ = 0f, VelocityY = 4f, DecayPerTick = 0.05f });
        }

        var positions = new List<(float, float, float)>();
        for (int tick = 0; tick < 5000; tick++)
            sys.Update(em, 1f);

        foreach (var e in em.GetAllEntities().OrderBy(e => e.Id))
        {
            if (e.Has<PositionComponent>())
            {
                var p = e.Get<PositionComponent>();
                positions.Add((p.X, p.Y, p.Z));
            }
        }
        return positions;
    }

    [Fact]
    public void AT11_FiveThousandTicks_SameState_AcrossSeeds()
    {
        var run1 = RunScenario(seed: 0);
        var run2 = RunScenario(seed: 0);

        Assert.Equal(run1.Count, run2.Count);
        for (int i = 0; i < run1.Count; i++)
        {
            Assert.Equal(run1[i].X, run2[i].X, 6);
            Assert.Equal(run1[i].Y, run2[i].Y, 6);
            Assert.Equal(run1[i].Z, run2[i].Z, 6);
        }
    }
}
