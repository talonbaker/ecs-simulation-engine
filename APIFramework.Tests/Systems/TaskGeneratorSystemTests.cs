using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>AT-02: TaskGeneratorSystem generation rate, assignment, and determinism.</summary>
public class TaskGeneratorSystemTests
{
    // Clock starts at 6 AM (GameHour=6.0). Default generation hour = 8.0.
    // Tick(60f) → +7200 game-seconds → GameHour=8.0 on Day 1.
    private static void AdvanceToGenerationHour(SimulationClock clock)
        => clock.Tick(60f);

    private static Entity AddNpcWithCapacity(EntityManager em, int capacity)
    {
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new WorkloadComponent
        {
            ActiveTasks = new List<Guid>(),
            Capacity    = capacity,
            CurrentLoad = 0
        });
        return npc;
    }

    // ── Generation timing ─────────────────────────────────────────────────────

    [Fact]
    public void BeforeGenerationHour_NoTasksCreated()
    {
        var em  = new EntityManager();
        var clock = new SimulationClock();   // GameHour = 6.0 < 8.0 → no trigger
        var sys = new TaskGeneratorSystem(new WorkloadConfig { TaskGenerationCountPerDay = 3 },
            clock, new SeededRandom(1));

        AddNpcWithCapacity(em, 5);
        sys.Update(em, 1f);

        Assert.Empty(em.Query<TaskTag>());
    }

    [Fact]
    public void AtGenerationHour_CreatesExactTaskCount()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var sys   = new TaskGeneratorSystem(new WorkloadConfig { TaskGenerationCountPerDay = 5 },
            clock, new SeededRandom(1));

        AddNpcWithCapacity(em, 10);
        AdvanceToGenerationHour(clock);
        sys.Update(em, 1f);

        Assert.Equal(5, em.Query<TaskTag>().Count());
    }

    [Fact]
    public void SameDay_SecondUpdate_DoesNotGenerateMore()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var sys   = new TaskGeneratorSystem(new WorkloadConfig { TaskGenerationCountPerDay = 5 },
            clock, new SeededRandom(1));

        AddNpcWithCapacity(em, 10);
        AdvanceToGenerationHour(clock);
        sys.Update(em, 1f);
        sys.Update(em, 1f); // same day, same hour — no re-trigger

        Assert.Equal(5, em.Query<TaskTag>().Count());
    }

    [Fact]
    public void NewDay_TriggersAgain()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var sys   = new TaskGeneratorSystem(new WorkloadConfig { TaskGenerationCountPerDay = 5 },
            clock, new SeededRandom(1));

        AddNpcWithCapacity(em, 10);

        // Day 1 generation
        AdvanceToGenerationHour(clock);
        sys.Update(em, 1f);
        Assert.Equal(5, em.Query<TaskTag>().Count());

        // Advance to day 2 at 8 AM: one full day (720f) + 0h (already past 8 AM offset)
        // Day transitions at midnight; advance 1 full day to be on day 2 at the same hour
        clock.Tick(720f); // +86400 game-seconds → new day, same hour relative offset
        sys.Update(em, 1f);

        Assert.Equal(10, em.Query<TaskTag>().Count());
    }

    // ── Round-robin assignment ────────────────────────────────────────────────

    [Fact]
    public void RoundRobin_RespectsCapacity_ExcessTasksUnassigned()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        // 2 NPCs each with capacity 1; generate 3 tasks → 2 assigned, 1 unassigned
        var sys = new TaskGeneratorSystem(new WorkloadConfig { TaskGenerationCountPerDay = 3 },
            clock, new SeededRandom(42));

        var npc1 = AddNpcWithCapacity(em, 1);
        var npc2 = AddNpcWithCapacity(em, 1);

        AdvanceToGenerationHour(clock);
        sys.Update(em, 1f);

        Assert.Equal(3, em.Query<TaskTag>().Count());
        Assert.Equal(1, npc1.Get<WorkloadComponent>().ActiveTasks?.Count ?? 0);
        Assert.Equal(1, npc2.Get<WorkloadComponent>().ActiveTasks?.Count ?? 0);

        var unassigned = em.Query<TaskTag>()
            .Where(e => e.Has<TaskComponent>() && e.Get<TaskComponent>().AssignedNpcId == Guid.Empty)
            .ToList();
        Assert.Single(unassigned);
    }

    [Fact]
    public void AllNpcsAtCapacity_AllTasksUnassigned()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var sys   = new TaskGeneratorSystem(new WorkloadConfig { TaskGenerationCountPerDay = 2 },
            clock, new SeededRandom(1));

        // NPC already at full capacity — pre-fill the workload
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new WorkloadComponent
        {
            ActiveTasks = new List<Guid> { Guid.NewGuid() },
            Capacity    = 1,  // at capacity
            CurrentLoad = 100
        });

        AdvanceToGenerationHour(clock);
        sys.Update(em, 1f);

        var tasks = em.Query<TaskTag>().ToList();
        Assert.Equal(2, tasks.Count);
        Assert.All(tasks, t => Assert.Equal(Guid.Empty, t.Get<TaskComponent>().AssignedNpcId));
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void SameSeed_SameTaskProperties()
    {
        static List<(int Priority, float EffortHours)> Run(int seed)
        {
            var em    = new EntityManager();
            var clock = new SimulationClock();
            var sys   = new TaskGeneratorSystem(new WorkloadConfig { TaskGenerationCountPerDay = 5 },
                clock, new SeededRandom(seed));
            var npc = em.CreateEntity();
            npc.Add(new NpcTag());
            npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid>(), Capacity = 10 });
            clock.Tick(60f);
            sys.Update(em, 1f);
            return em.Query<TaskTag>()
                .Select(e => e.Get<TaskComponent>())
                .Select(tc => (tc.Priority, tc.EffortHours))
                .OrderBy(t => t.Priority).ThenBy(t => t.EffortHours)
                .ToList();
        }

        var run1 = Run(777);
        var run2 = Run(777);

        Assert.Equal(run1.Count, run2.Count);
        for (int i = 0; i < run1.Count; i++)
        {
            Assert.Equal(run1[i].Priority,    run2[i].Priority);
            Assert.Equal(run1[i].EffortHours, run2[i].EffortHours);
        }
    }

    [Fact]
    public void DifferentSeeds_DifferentTaskProperties()
    {
        static List<int> Run(int seed)
        {
            var em    = new EntityManager();
            var clock = new SimulationClock();
            var sys   = new TaskGeneratorSystem(new WorkloadConfig { TaskGenerationCountPerDay = 5 },
                clock, new SeededRandom(seed));
            var npc = em.CreateEntity();
            npc.Add(new NpcTag());
            npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid>(), Capacity = 10 });
            clock.Tick(60f);
            sys.Update(em, 1f);
            return em.Query<TaskTag>()
                .Select(e => e.Get<TaskComponent>().Priority)
                .OrderBy(p => p).ToList();
        }

        var run1 = Run(1);
        var run2 = Run(2);
        // Different seeds should produce at least one differing priority value
        Assert.True(run1.Count != run2.Count || run1.Zip(run2).Any(p => p.First != p.Second),
            "Different seeds should produce different task priorities");
    }
}
