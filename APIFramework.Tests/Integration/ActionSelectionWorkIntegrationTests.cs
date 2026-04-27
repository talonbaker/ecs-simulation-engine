using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Integration;

/// <summary>AT-08: NPC with active task + AtDesk schedule + quiet drives → Work(taskEntity).</summary>
public class ActionSelectionWorkIntegrationTests
{
    private static ActionSelectionConfig DefaultCfg() => new()
    {
        DriveCandidateThreshold        = 60,
        IdleScoreFloor                 = 0.20,
        InversionStakeThreshold        = 0.55,
        InversionInhibitionThreshold   = 0.50,
        SuppressionGiveUpFactor        = 0.30,
        SuppressionEpsilon             = 0.10,
        SuppressionEventMagnitudeScale = 5,
        PersonalityTieBreakWeight      = 0.05,
        MaxCandidatesPerTick           = 32,
        AvoidStandoffDistance          = 4
    };

    private static ScheduleConfig SchedCfg() => new()
    {
        ScheduleAnchorBaseWeight     = 0.30,
        ScheduleLingerThresholdCells = 2.0f
    };

    private static GridSpatialIndex MakeSpatial() =>
        new(new SpatialConfig { CellSizeTiles = 4, WorldSize = new() { Width = 64, Height = 64 } });

    [Fact]
    public void AtDesk_WithActiveTasks_QuietDrives_ProducesWorkIntent()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { Priority = 50, Progress = 0.1f, QualityLevel = 1f });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new WillpowerComponent(50, 50));
        npc.Add(new SocialDrivesComponent()); // all drives at 0
        npc.Add(new WorkloadComponent
        {
            ActiveTasks = new List<Guid> { task.Id },
            Capacity    = 3,
            CurrentLoad = 33
        });
        npc.Add(new CurrentScheduleBlockComponent
        {
            ActiveBlockIndex = 0,
            AnchorEntityId   = Guid.Empty,  // no schedule anchor → no competing Approach candidate
            Activity         = ScheduleActivityKind.AtDesk
        });
        spatial.Register(npc, 5, 5);

        var sys = new ActionSelectionSystem(spatial, new EntityRoomMembership(), queue,
            new SeededRandom(42), DefaultCfg(), SchedCfg(), em,
            new WorkloadConfig { WorkActionBaseWeight = 0.40 });

        sys.Update(em, 1f);

        Assert.True(npc.Has<IntendedActionComponent>());
        var intent = npc.Get<IntendedActionComponent>();
        Assert.Equal(IntendedActionKind.Work, intent.Kind);
        Assert.Equal(WillpowerSystem.EntityIntId(task), intent.TargetEntityId);
    }

    [Fact]
    public void NoActiveTasks_DoesNotProduceWorkIntent()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new WillpowerComponent(50, 50));
        npc.Add(new SocialDrivesComponent());
        npc.Add(new WorkloadComponent { ActiveTasks = Array.Empty<Guid>(), Capacity = 3 });
        npc.Add(new CurrentScheduleBlockComponent
        {
            ActiveBlockIndex = 0,
            AnchorEntityId   = Guid.Empty,
            Activity         = ScheduleActivityKind.AtDesk
        });
        spatial.Register(npc, 5, 5);

        var sys = new ActionSelectionSystem(spatial, new EntityRoomMembership(), queue,
            new SeededRandom(42), DefaultCfg(), SchedCfg(), em);

        sys.Update(em, 1f);

        var intent = npc.Get<IntendedActionComponent>();
        Assert.NotEqual(IntendedActionKind.Work, intent.Kind);
    }

    [Fact]
    public void NotAtDesk_DoesNotProduceWorkIntent()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { Priority = 50, Progress = 0.1f });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new WillpowerComponent(50, 50));
        npc.Add(new SocialDrivesComponent());
        npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { task.Id }, Capacity = 3 });
        npc.Add(new CurrentScheduleBlockComponent
        {
            ActiveBlockIndex = 0,
            AnchorEntityId   = Guid.Empty,
            Activity         = ScheduleActivityKind.Lunch   // not AtDesk
        });
        spatial.Register(npc, 5, 5);

        var sys = new ActionSelectionSystem(spatial, new EntityRoomMembership(), queue,
            new SeededRandom(42), DefaultCfg(), SchedCfg(), em);

        sys.Update(em, 1f);

        Assert.NotEqual(IntendedActionKind.Work, npc.Get<IntendedActionComponent>().Kind);
    }

    [Fact]
    public void HighPriorityTask_IsSelectedAsTarget()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        var lowTask = em.CreateEntity();
        lowTask.Add(new TaskTag());
        lowTask.Add(new TaskComponent { Priority = 20, Progress = 0f });

        var highTask = em.CreateEntity();
        highTask.Add(new TaskTag());
        highTask.Add(new TaskComponent { Priority = 80, Progress = 0.1f });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new WillpowerComponent(50, 50));
        npc.Add(new SocialDrivesComponent());
        npc.Add(new WorkloadComponent
        {
            ActiveTasks = new List<Guid> { lowTask.Id, highTask.Id },
            Capacity    = 3
        });
        npc.Add(new CurrentScheduleBlockComponent
        {
            ActiveBlockIndex = 0,
            AnchorEntityId   = Guid.Empty,
            Activity         = ScheduleActivityKind.AtDesk
        });
        spatial.Register(npc, 5, 5);

        var sys = new ActionSelectionSystem(spatial, new EntityRoomMembership(), queue,
            new SeededRandom(42), DefaultCfg(), SchedCfg(), em,
            new WorkloadConfig { WorkActionBaseWeight = 0.40 });

        sys.Update(em, 1f);

        var intent = npc.Get<IntendedActionComponent>();
        Assert.Equal(IntendedActionKind.Work, intent.Kind);
        Assert.Equal(WillpowerSystem.EntityIntId(highTask), intent.TargetEntityId);
    }
}
