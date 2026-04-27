using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>AT-05: Completed tasks are removed from ActiveTasks and TaskCompleted candidate emitted.</summary>
public class WorkloadSystemCompletionTests
{
    [Fact]
    public void TaskReachesMaxProgress_RemovedFromActiveTasks()
    {
        var em    = new EntityManager();
        var cfg   = new WorkloadConfig { BaseProgressRatePerSecond = 2.0 }; // 0.9 + 2.0*1f > 1.0
        var sys   = new WorkloadSystem(cfg, new SimulationClock(), new NarrativeEventBus(), em);

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { Progress = 0.9f, QualityLevel = 1f, Priority = 50 });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        npc.Add(new EnergyComponent { Energy = 100f });
        npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { task.Id }, Capacity = 3 });
        npc.Add(new IntendedActionComponent(
            IntendedActionKind.Work, WillpowerSystem.EntityIntId(task),
            DialogContextValue.None, 0));

        sys.Update(em, 1f);

        Assert.Empty(npc.Get<WorkloadComponent>().ActiveTasks ?? Array.Empty<Guid>());
    }

    [Fact]
    public void TaskCompletion_DestroyesTaskEntity()
    {
        var em  = new EntityManager();
        var cfg = new WorkloadConfig { BaseProgressRatePerSecond = 2.0 };
        var sys = new WorkloadSystem(cfg, new SimulationClock(), new NarrativeEventBus(), em);

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { Progress = 0.9f, QualityLevel = 1f, Priority = 50 });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        npc.Add(new EnergyComponent { Energy = 100f });
        npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { task.Id }, Capacity = 3 });
        npc.Add(new IntendedActionComponent(
            IntendedActionKind.Work, WillpowerSystem.EntityIntId(task),
            DialogContextValue.None, 0));

        sys.Update(em, 1f);

        Assert.Empty(em.Query<TaskTag>());
    }

    [Fact]
    public void TaskCompletion_EmitsTaskCompletedCandidate()
    {
        var em    = new EntityManager();
        var bus   = new NarrativeEventBus();
        var cfg   = new WorkloadConfig { BaseProgressRatePerSecond = 2.0 };
        var sys   = new WorkloadSystem(cfg, new SimulationClock(), bus, em);

        var emitted = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += c => emitted.Add(c);

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { Progress = 0.9f, QualityLevel = 1f, Priority = 50 });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        npc.Add(new EnergyComponent { Energy = 100f });
        npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { task.Id }, Capacity = 3 });
        npc.Add(new IntendedActionComponent(
            IntendedActionKind.Work, WillpowerSystem.EntityIntId(task),
            DialogContextValue.None, 0));

        sys.Update(em, 1f);

        Assert.Single(emitted);
        Assert.Equal(NarrativeEventKind.TaskCompleted, emitted[0].Kind);
        Assert.Contains(WillpowerSystem.EntityIntId(npc), emitted[0].ParticipantIds);
    }

    [Fact]
    public void InProgressTask_NotRemovedPrematurely()
    {
        var em  = new EntityManager();
        var cfg = new WorkloadConfig { BaseProgressRatePerSecond = 0.001 }; // very slow
        var sys = new WorkloadSystem(cfg, new SimulationClock(), new NarrativeEventBus(), em);

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { Progress = 0.1f, QualityLevel = 1f });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        npc.Add(new EnergyComponent { Energy = 100f });
        npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { task.Id }, Capacity = 3 });
        npc.Add(new IntendedActionComponent(
            IntendedActionKind.Work, WillpowerSystem.EntityIntId(task),
            DialogContextValue.None, 0));

        sys.Update(em, 1f);

        // 0.1 + 0.001 << 1.0 — task still active
        Assert.Single(npc.Get<WorkloadComponent>().ActiveTasks ?? Array.Empty<Guid>());
        Assert.Single(em.Query<TaskTag>());
    }
}
