using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>AT-03/AT-04: WorkloadSystem advances progress; physiology and stress modulate rate.</summary>
public class WorkloadSystemProgressTests
{
    private static (EntityManager em, Entity npc, Entity task, WorkloadSystem sys)
        BuildWellRested(double baseRate = 0.01)
    {
        var cfg = new WorkloadConfig { BaseProgressRatePerSecond = baseRate };
        var em  = new EntityManager();
        var sys = new WorkloadSystem(cfg, new SimulationClock(), new NarrativeEventBus(), em);

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { Progress = 0f, QualityLevel = 1f, Priority = 50 });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        npc.Add(new EnergyComponent { Energy = 100f });
        npc.Add(new WorkloadComponent
        {
            ActiveTasks = new List<Guid> { task.Id },
            Capacity    = 3
        });
        npc.Add(new IntendedActionComponent(
            IntendedActionKind.Work, WillpowerSystem.EntityIntId(task),
            DialogContextValue.None, 0));

        return (em, npc, task, sys);
    }

    // -- AT-03: Progress advancement -------------------------------------------

    [Fact]
    public void WorkIntent_AdvancesTaskProgress()
    {
        var (em, _, task, sys) = BuildWellRested();

        sys.Update(em, 1f);

        Assert.True(task.Get<TaskComponent>().Progress > 0f);
    }

    [Fact]
    public void ProgressRate_MatchesBaseRateTimesDelta()
    {
        // All multipliers = 1.0 → progress = baseRate * deltaTime = 0.01 * 1.0
        var (em, _, task, sys) = BuildWellRested(baseRate: 0.01);

        sys.Update(em, 1f);

        Assert.Equal(0.01f, task.Get<TaskComponent>().Progress, precision: 6);
    }

    [Fact]
    public void NoWorkIntent_NoProgressAdvanced()
    {
        var (em, npc, task, sys) = BuildWellRested();

        npc.Add(new IntendedActionComponent(IntendedActionKind.Idle, 0, DialogContextValue.None, 0));
        sys.Update(em, 1f);

        Assert.Equal(0f, task.Get<TaskComponent>().Progress);
    }

    [Fact]
    public void ProgressAccumulates_OverMultipleTicks()
    {
        var cfg = new WorkloadConfig { BaseProgressRatePerSecond = 0.01 };
        var (em, _, task, sys) = BuildWellRested(baseRate: 0.01);

        for (int i = 0; i < 10; i++)
            sys.Update(em, 1f);

        // 10 * 0.01 * 1.0 = 0.10
        Assert.Equal(0.10f, task.Get<TaskComponent>().Progress, precision: 5);
    }

    // -- AT-04: Physiology modulation ------------------------------------------

    [Fact]
    public void WellRested_ProgressesFaster_ThanTiredHungry()
    {
        var cfg = new WorkloadConfig { BaseProgressRatePerSecond = 0.0001 };

        // Well-rested: energy=100, no tags → physiologyMult = 1.0
        var emA  = new EntityManager();
        var sysA = new WorkloadSystem(cfg, new SimulationClock(), new NarrativeEventBus(), emA);
        var taskA = emA.CreateEntity();
        taskA.Add(new TaskTag());
        taskA.Add(new TaskComponent { Progress = 0f, QualityLevel = 1f });
        var npcA = emA.CreateEntity();
        npcA.Add(new NpcTag());
        npcA.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        npcA.Add(new EnergyComponent { Energy = 100f });
        npcA.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { taskA.Id }, Capacity = 3 });
        npcA.Add(new IntendedActionComponent(IntendedActionKind.Work,
            WillpowerSystem.EntityIntId(taskA), DialogContextValue.None, 0));

        // Tired + hungry: energy=20, HungryTag → physiologyMult = (20/100) * 0.7 = 0.14
        var emB  = new EntityManager();
        var sysB = new WorkloadSystem(cfg, new SimulationClock(), new NarrativeEventBus(), emB);
        var taskB = emB.CreateEntity();
        taskB.Add(new TaskTag());
        taskB.Add(new TaskComponent { Progress = 0f, QualityLevel = 1f });
        var npcB = emB.CreateEntity();
        npcB.Add(new NpcTag());
        npcB.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        npcB.Add(new EnergyComponent { Energy = 20f });
        npcB.Add(new HungryTag());
        npcB.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { taskB.Id }, Capacity = 3 });
        npcB.Add(new IntendedActionComponent(IntendedActionKind.Work,
            WillpowerSystem.EntityIntId(taskB), DialogContextValue.None, 0));

        for (int i = 0; i < 1000; i++)
        {
            sysA.Update(emA, 1f);
            sysB.Update(emB, 1f);
        }

        float progA = taskA.Get<TaskComponent>().Progress;
        float progB = taskB.Get<TaskComponent>().Progress;

        Assert.True(progA > progB,
            $"Well-rested ({progA:F4}) should have more progress than tired/hungry ({progB:F4})");
    }

    [Fact]
    public void StressedNpc_ProgressesSlower_ThanCalm()
    {
        var cfg = new WorkloadConfig { BaseProgressRatePerSecond = 0.0001 };

        static (EntityManager em, WorkloadSystem sys, Entity task) Make(WorkloadConfig c, bool stressed)
        {
            var emX  = new EntityManager();
            var sysX = new WorkloadSystem(c, new SimulationClock(), new NarrativeEventBus(), emX);
            var t = emX.CreateEntity();
            t.Add(new TaskTag());
            t.Add(new TaskComponent { Progress = 0f, QualityLevel = 1f });
            var n = emX.CreateEntity();
            n.Add(new NpcTag());
            n.Add(new PersonalityComponent(0, 0, 0, 0, 0));
            n.Add(new EnergyComponent { Energy = 100f });
            if (stressed) n.Add(new StressedTag());
            n.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { t.Id }, Capacity = 3 });
            n.Add(new IntendedActionComponent(IntendedActionKind.Work,
                WillpowerSystem.EntityIntId(t), DialogContextValue.None, 0));
            return (emX, sysX, t);
        }

        var (emC, sysC, taskCalm)    = Make(cfg, stressed: false);
        var (emS, sysS, taskStressed) = Make(cfg, stressed: true);

        for (int i = 0; i < 100; i++)
        {
            sysC.Update(emC, 1f);
            sysS.Update(emS, 1f);
        }

        Assert.True(taskCalm.Get<TaskComponent>().Progress > taskStressed.Get<TaskComponent>().Progress,
            "Calm NPC should advance task progress faster than stressed NPC");
    }

    [Fact]
    public void OverwhelmedNpc_ProgressesSlower_ThanStressed()
    {
        var cfg = new WorkloadConfig { BaseProgressRatePerSecond = 0.0001 };

        static (EntityManager em, WorkloadSystem sys, Entity task) Make(WorkloadConfig c, bool overwhelmed)
        {
            var emX  = new EntityManager();
            var sysX = new WorkloadSystem(c, new SimulationClock(), new NarrativeEventBus(), emX);
            var t = emX.CreateEntity();
            t.Add(new TaskTag());
            t.Add(new TaskComponent { Progress = 0f, QualityLevel = 1f });
            var n = emX.CreateEntity();
            n.Add(new NpcTag());
            n.Add(new PersonalityComponent(0, 0, 0, 0, 0));
            n.Add(new EnergyComponent { Energy = 100f });
            if (overwhelmed) n.Add(new OverwhelmedTag());
            else             n.Add(new StressedTag());
            n.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { t.Id }, Capacity = 3 });
            n.Add(new IntendedActionComponent(IntendedActionKind.Work,
                WillpowerSystem.EntityIntId(t), DialogContextValue.None, 0));
            return (emX, sysX, t);
        }

        var (emO, sysO, taskOverwhelmed) = Make(cfg, overwhelmed: true);
        var (emS, sysS, taskStressed)    = Make(cfg, overwhelmed: false);

        for (int i = 0; i < 100; i++)
        {
            sysO.Update(emO, 1f);
            sysS.Update(emS, 1f);
        }

        // stressMult: Overwhelmed=0.5 < Stressed=0.8
        Assert.True(taskStressed.Get<TaskComponent>().Progress > taskOverwhelmed.Get<TaskComponent>().Progress,
            "Stressed NPC (mult=0.8) should progress faster than overwhelmed (mult=0.5)");
    }
}
