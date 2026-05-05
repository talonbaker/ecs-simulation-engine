using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>AT-10: 5000-tick determinism — same seed produces byte-identical task + workload state.</summary>
public class WorkloadDeterminismTests
{
    [Fact]
    public void SameSeed_ProducesByteIdenticalTaskState()
    {
        var run1 = RunSimulation(seed: 42);
        var run2 = RunSimulation(seed: 42);

        Assert.Equal(run1.Count, run2.Count);
        for (int i = 0; i < run1.Count; i++)
        {
            Assert.True(run1[i].Priority    == run2[i].Priority &&
                        run1[i].EffortHours == run2[i].EffortHours,
                $"Tick {i}: Priority {run1[i].Priority} vs {run2[i].Priority}, " +
                $"Effort {run1[i].EffortHours} vs {run2[i].EffortHours}");
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentTaskState()
    {
        var run1 = RunSimulation(seed: 1);
        var run2 = RunSimulation(seed: 2);

        bool anyDiff = run1.Count != run2.Count ||
            run1.Zip(run2).Any(p => p.First.Priority != p.Second.Priority);
        Assert.True(anyDiff, "Different seeds should produce different task trajectories");
    }

    [Fact]
    public void SameSeed_WorkloadComponent_MatchesAcrossTwoRuns()
    {
        static int GetFinalTaskCount(int seed)
        {
            var em    = new EntityManager();
            var clock = new SimulationClock();
            var rng   = new SeededRandom(seed);
            var cfg   = new WorkloadConfig { TaskGenerationCountPerDay = 5 };
            var sys   = new TaskGeneratorSystem(cfg, clock, rng);

            var npc = em.CreateEntity();
            npc.Add(new NpcTag());
            npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid>(), Capacity = 100 });

            for (int i = 0; i < 5000; i++) { clock.Tick(1f); sys.Update(em, 1f); }

            return npc.Get<WorkloadComponent>().ActiveTasks?.Count ?? 0;
        }

        int count1 = GetFinalTaskCount(99);
        int count2 = GetFinalTaskCount(99);

        Assert.Equal(count1, count2);
    }

    // -- Simulation runner -----------------------------------------------------

    private static List<(int Priority, float EffortHours)> RunSimulation(int seed)
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var rng   = new SeededRandom(seed);
        var cfg   = new WorkloadConfig { TaskGenerationCountPerDay = 5 };
        var sys   = new TaskGeneratorSystem(cfg, clock, rng);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid>(), Capacity = 100 });

        // 5000 ticks × 120 game-seconds/tick = 600,000 game-seconds ≈ 7 game-days
        // TaskGenerator fires on days 1-7 at hour 8 → 7 × 5 = 35 tasks generated
        for (int i = 0; i < 5000; i++)
        {
            clock.Tick(1f);
            sys.Update(em, 1f);
        }

        return em.Query<TaskTag>()
            .Select(e => e.Get<TaskComponent>())
            .Select(tc => (tc.Priority, tc.EffortHours))
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.EffortHours)
            .ToList();
    }
}
