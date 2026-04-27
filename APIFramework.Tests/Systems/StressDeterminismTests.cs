using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>AT-11: 5000-tick determinism — same seed → byte-identical StressComponent state.</summary>
public class StressDeterminismTests
{
    [Fact]
    public void SameSeed_ProducesBytIdenticalStressState()
    {
        const int seed  = 1337;
        const int ticks = 5000;

        var run1 = RunSimulation(seed, ticks);
        var run2 = RunSimulation(seed, ticks);

        Assert.Equal(run1.Count, run2.Count);
        for (int i = 0; i < run1.Count; i++)
        {
            var (a1, c1) = run1[i];
            var (a2, c2) = run2[i];
            Assert.True(a1 == a2 && c1 == c2,
                $"Tick {i}: AcuteLevel {a1} vs {a2}, ChronicLevel {c1:F6} vs {c2:F6}");
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentTrajectories()
    {
        const int ticks = 5000;
        var run1 = RunSimulation(seed: 1, ticks);
        var run2 = RunSimulation(seed: 2, ticks);

        bool anyDiff = false;
        for (int i = 0; i < run1.Count; i++)
        {
            if (run1[i] != run2[i]) { anyDiff = true; break; }
        }
        Assert.True(anyDiff, "Different seeds should produce different stress trajectories");
    }

    private static List<(int Acute, double Chronic)> RunSimulation(int seed, int ticks)
    {
        var stressCfg = new StressConfig();
        var socialCfg = new SocialSystemConfig();
        var clock     = new SimulationClock();
        var queue     = new WillpowerEventQueue();
        var bus       = new NarrativeEventBus();
        var rng       = new SeededRandom(seed);

        var driveSys  = new DriveDynamicsSystem(socialCfg, clock, rng, stressCfg);
        var wpSys     = new WillpowerSystem(socialCfg, queue);
        var em        = new EntityManager();
        var stressSys = new StressSystem(stressCfg, new WorkloadConfig(), clock, queue, bus, em);
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 1));   // Neuroticism=1 for variance
        npc.Add(new WillpowerComponent(80, 80));
        npc.Add(new SocialDrivesComponent
        {
            Loneliness  = new DriveValue { Current = 70, Baseline = 50 },  // spike-prone
            Irritation  = new DriveValue { Current = 80, Baseline = 50 },
        });
        npc.Add(new StressComponent { AcuteLevel = 30, LastDayUpdated = 1 });

        var trajectory = new List<(int, double)>(ticks);

        for (int i = 0; i < ticks; i++)
        {
            clock.Tick(1f / clock.TimeScale);

            driveSys.Update(em, 1f);
            wpSys.Update(em, 1f);    // drains queue → sets LastDrainedBatch
            stressSys.Update(em, 1f);

            var s = npc.Get<StressComponent>();
            trajectory.Add((s.AcuteLevel, s.ChronicLevel));
        }

        return trajectory;
    }
}
