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

/// <summary>AT-06: Past-deadline tasks gain OverdueTag and emit OverdueTask candidate exactly once.</summary>
public class WorkloadSystemOverdueTests
{
    [Fact]
    public void TaskPastDeadline_GainsOverdueTag()
    {
        var em  = new EntityManager();
        var sys = new WorkloadSystem(new WorkloadConfig(), new SimulationClock(),
            new NarrativeEventBus(), em);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        // DeadlineTick = -1: TotalTime(0) > -1 immediately
        task.Add(new TaskComponent { DeadlineTick = -1L, Progress = 0f, Priority = 50 });

        npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { task.Id }, Capacity = 3 });

        sys.Update(em, 1f);

        Assert.True(task.Has<OverdueTag>());
    }

    [Fact]
    public void OverdueTask_EmitsOverdueCandidate_WithCorrectParticipant()
    {
        var em  = new EntityManager();
        var bus = new NarrativeEventBus();
        var sys = new WorkloadSystem(new WorkloadConfig(), new SimulationClock(), bus, em);

        var emitted = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += c => emitted.Add(c);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { DeadlineTick = -1L, Progress = 0f, Priority = 50 });

        npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { task.Id }, Capacity = 3 });

        sys.Update(em, 1f);

        var overdueEvents = emitted.Where(c => c.Kind == NarrativeEventKind.OverdueTask).ToList();
        Assert.Single(overdueEvents);
        Assert.Contains(WillpowerSystem.EntityIntId(npc), overdueEvents[0].ParticipantIds);
    }

    [Fact]
    public void OverdueTask_CandidateEmittedOnce_NotEveryTick()
    {
        var em  = new EntityManager();
        var bus = new NarrativeEventBus();
        var sys = new WorkloadSystem(new WorkloadConfig(), new SimulationClock(), bus, em);

        var emitted = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += c => emitted.Add(c);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { DeadlineTick = -1L, Progress = 0f, Priority = 50 });

        npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { task.Id }, Capacity = 3 });

        sys.Update(em, 1f); // first tick: OverdueTag added, 1 candidate emitted
        sys.Update(em, 1f); // second tick: OverdueTag already present, no new candidate
        sys.Update(em, 1f); // third tick: same — still no new candidate

        Assert.Single(emitted, c => c.Kind == NarrativeEventKind.OverdueTask);
    }

    [Fact]
    public void TaskNotYetDue_NoOverdueTag()
    {
        var em  = new EntityManager();
        var sys = new WorkloadSystem(new WorkloadConfig(), new SimulationClock(),
            new NarrativeEventBus(), em);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { DeadlineTick = 999999L, Progress = 0f });

        npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { task.Id }, Capacity = 3 });

        sys.Update(em, 1f);

        Assert.False(task.Has<OverdueTag>());
    }
}
