using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-10: Two runs with the same seed produce byte-identical drive trajectories over 5000 ticks.
/// Verifies the determinism contract for the social drive system.
/// </summary>
public class SocialDeterminismTests
{
    [Fact]
    public void DriveDynamics_TwoRunsSameSeed_ProduceBytIdenticalTrajectories()
    {
        const int seed  = 7777;
        const int ticks = 5000;

        var trajectory1 = RunTrajectory(seed, ticks);
        var trajectory2 = RunTrajectory(seed, ticks);

        Assert.Equal(trajectory1.Count, trajectory2.Count);
        for (int i = 0; i < trajectory1.Count; i++)
        {
            Assert.True(trajectory1[i] == trajectory2[i],
                $"Tick {i}: trajectory diverged ({trajectory1[i]} vs {trajectory2[i]})");
        }
    }

    [Fact]
    public void DriveDynamics_DifferentSeeds_ProduceDifferentTrajectories()
    {
        const int ticks = 5000;
        var t1 = RunTrajectory(seed: 1, ticks);
        var t2 = RunTrajectory(seed: 2, ticks);

        // With different seeds, at least some ticks should differ
        bool anyDifference = false;
        for (int i = 0; i < t1.Count; i++)
        {
            if (t1[i] != t2[i]) { anyDifference = true; break; }
        }
        Assert.True(anyDifference, "Different seeds should produce different trajectories");
    }

    private static List<int> RunTrajectory(int seed, int ticks)
    {
        var cfg = new SocialSystemConfig();
        var clock = new SimulationClock();
        var rng   = new SeededRandom(seed);
        var sys   = new DriveDynamicsSystem(cfg, clock, rng);

        var em = new EntityManager();
        var e  = em.CreateEntity();
        e.Add(new NpcTag());
        e.Add(new SocialDrivesComponent
        {
            Loneliness = new DriveValue { Current = 50, Baseline = 50 }
        });
        e.Add(new PersonalityComponent(0, 0, 0, 0, 1));  // Neuroticism +1 for variance

        var trajectory = new List<int>(ticks);
        for (int i = 0; i < ticks; i++)
        {
            clock.Tick(1f / clock.TimeScale);  // advance one real-second tick
            sys.Update(em, 1f);
            trajectory.Add(e.Get<SocialDrivesComponent>().Loneliness.Current);
        }
        return trajectory;
    }
}
