using System;
using System.Diagnostics;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Spatial;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.Spatial;

/// <summary>
/// AT-08 (FPS gate): SpatialBehaviorSystem must sustain ≥ 1000 ticks/sec with 30 NPCs.
/// 1000 ticks/sec is 16× the 60 FPS real-time bound — leaves ample headroom.
/// </summary>
public class Spatial30NpcFpsTests
{
    [Fact]
    public void AT08_30Npcs_Sustains1000TicksPerSecond()
    {
        var em  = new EntityManager();
        var sys = new SpatialBehaviorSystem();

        // Spawn 30 NPCs spread across a 5×5 cluster.
        var rng = new Random(42);
        for (int i = 0; i < 30; i++)
        {
            var e = em.CreateEntity();
            e.Add(new NpcTag());
            e.Add(new PositionComponent
            {
                X = (float)(rng.NextDouble() * 5),
                Z = (float)(rng.NextDouble() * 5)
            });
            e.Add(new PersonalSpaceComponent { RadiusMeters = 0.6f, RepulsionStrength = 0.3f });
            e.Add(new LifeStateComponent { State = LS.Alive });
        }

        const int warmupTicks = 100;
        const int timedTicks  = 2000;

        // Warm-up.
        for (int t = 0; t < warmupTicks; t++)
            sys.Update(em, 1f);

        // Timed run.
        var sw = Stopwatch.StartNew();
        for (int t = 0; t < timedTicks; t++)
            sys.Update(em, 1f);
        sw.Stop();

        double ticksPerSec = timedTicks / sw.Elapsed.TotalSeconds;
        Assert.True(ticksPerSec >= 1000.0,
            $"SpatialBehaviorSystem achieved only {ticksPerSec:F0} ticks/sec with 30 NPCs; " +
            $"minimum required is 1000.");
    }
}
