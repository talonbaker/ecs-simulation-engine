using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Integration;

/// <summary>AT-07: NPC with overdue tasks accumulates AcuteLevel at the configured gain rate.</summary>
public class WorkloadStressIntegrationTests
{
    [Fact]
    public void ThreeOverdueTasks_AccumulateCorrectStressGain()
    {
        var em       = new EntityManager();
        var clock    = new SimulationClock();
        var queue    = new WillpowerEventQueue();
        var bus      = new NarrativeEventBus();
        var stressCfg = new StressConfig { AcuteDecayPerTick = 0.0 };
        var workCfg  = new WorkloadConfig { OverdueTaskStressGain = 1.0 };
        var sys      = new StressSystem(stressCfg, workCfg, clock, queue, bus, em);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0)); // neuroFactor = 1.0
        npc.Add(new StressComponent { AcuteLevel = 0, LastDayUpdated = 1 });

        var taskGuids = new List<Guid>();
        for (int i = 0; i < 3; i++)
        {
            var task = em.CreateEntity();
            task.Add(new TaskTag());
            task.Add(new TaskComponent { DeadlineTick = -1L, Progress = 0f });
            task.Add(new OverdueTag());
            taskGuids.Add(task.Id);
        }
        npc.Add(new WorkloadComponent { ActiveTasks = taskGuids, Capacity = 5, CurrentLoad = 60 });

        sys.Update(em, 1f);

        var stress = npc.Get<StressComponent>();
        // 3 overdue * 1.0 gain * 1.0 neuroFactor = 3.0 → AcuteLevel = (int)3.0 = 3
        Assert.Equal(3, stress.AcuteLevel);
        Assert.Equal(3, stress.OverdueTaskEventsToday);
    }

    [Fact]
    public void ZeroOverdueTasks_NoStressGain()
    {
        var em       = new EntityManager();
        var clock    = new SimulationClock();
        var queue    = new WillpowerEventQueue();
        var bus      = new NarrativeEventBus();
        var sys      = new StressSystem(new StressConfig { AcuteDecayPerTick = 0.0 },
            new WorkloadConfig(), clock, queue, bus, em);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        npc.Add(new StressComponent { AcuteLevel = 5, LastDayUpdated = 1 });

        // Task exists but has NO OverdueTag
        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { DeadlineTick = 999999L, Progress = 0f });

        npc.Add(new WorkloadComponent
        {
            ActiveTasks = new List<Guid> { task.Id },
            Capacity    = 3
        });

        sys.Update(em, 1f);

        // No overdue gain; AcuteLevel should stay at 5 (no decay with AcuteDecayPerTick=0)
        Assert.Equal(5, npc.Get<StressComponent>().AcuteLevel);
        Assert.Equal(0, npc.Get<StressComponent>().OverdueTaskEventsToday);
    }

    [Fact]
    public void OverdueStress_ScalesWithNeuroticism()
    {
        var em       = new EntityManager();
        var clock    = new SimulationClock();
        var queue    = new WillpowerEventQueue();
        var bus      = new NarrativeEventBus();
        var stressCfg = new StressConfig
        {
            AcuteDecayPerTick       = 0.0,
            NeuroticismStressFactor = 0.2,
        };
        var sys = new StressSystem(stressCfg, new WorkloadConfig { OverdueTaskStressGain = 1.0 },
            clock, queue, bus, em);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 2)); // neuroticism=+2 → neuroFactor=1.4
        npc.Add(new StressComponent { AcuteLevel = 0, LastDayUpdated = 1 });

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { DeadlineTick = -1L, Progress = 0f });
        task.Add(new OverdueTag());

        npc.Add(new WorkloadComponent
        {
            ActiveTasks = new List<Guid> { task.Id },
            Capacity    = 3
        });

        sys.Update(em, 1f);

        var stress = npc.Get<StressComponent>();
        // 1 overdue * 1.0 gain * 1.4 neuroFactor = 1.4 → (int)1.4 = 1
        Assert.Equal(1, stress.AcuteLevel);
    }

    [Fact]
    public void OverdueTaskEvents_ResetOnDayAdvance()
    {
        var clock = new SimulationClock();
        var em    = new EntityManager();
        var queue = new WillpowerEventQueue();
        var bus   = new NarrativeEventBus();
        var sys   = new StressSystem(
            new StressConfig { AcuteDecayPerTick = 0.0 },
            new WorkloadConfig { OverdueTaskStressGain = 1.0 },
            clock, queue, bus, em);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        npc.Add(new StressComponent { AcuteLevel = 0, LastDayUpdated = 1 });

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { DeadlineTick = -1L, Progress = 0f });
        task.Add(new OverdueTag());

        npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { task.Id }, Capacity = 3 });

        // Day 1 tick — sets OverdueTaskEventsToday = 1
        sys.Update(em, 1f);
        Assert.Equal(1, npc.Get<StressComponent>().OverdueTaskEventsToday);

        // Advance to day 2 — the first tick on day 2 increments then resets (reset fires after count)
        clock.Tick(720f); // +86400 game-seconds → DayNumber = 2
        sys.Update(em, 1f); // increments to 2, then daily reset fires → 0

        Assert.Equal(0, npc.Get<StressComponent>().OverdueTaskEventsToday);
        Assert.Equal(2, npc.Get<StressComponent>().LastDayUpdated);

        // Second tick on day 2 — counter restarts from 0 (no reset this tick)
        sys.Update(em, 1f);
        Assert.Equal(1, npc.Get<StressComponent>().OverdueTaskEventsToday);
    }
}
