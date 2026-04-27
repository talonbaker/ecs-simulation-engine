using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Integration;

/// <summary>AT-09: Drive overrides Work; Work overrides Idle.</summary>
public class ActionSelectionWorkOverridingScheduleTests
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

    // ── AT-09a: Drive overrides Work ──────────────────────────────────────────

    [Fact]
    public void HighIrritationWithCoworker_OverridesWork_ProducesDialogLashOut()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { Priority = 50, Progress = 0.1f });

        // Coworker 1 tile away — required for LashOut (NeedsTarget = true)
        var coworker = em.CreateEntity();
        coworker.Add(new NpcTag());
        coworker.Add(new PositionComponent { X = 6f, Y = 0f, Z = 5f });
        spatial.Register(coworker, 6, 5);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new WillpowerComponent(60, 60));
        npc.Add(new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = 80, Baseline = 80 }
        });
        // No ConflictInhibition → LashOut weight ≈ 0.80+, Work weight = 0.40
        npc.Add(new WorkloadComponent
        {
            ActiveTasks = new List<Guid> { task.Id },
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
        Assert.Equal(IntendedActionKind.Dialog, intent.Kind);
        Assert.Equal(DialogContextValue.LashOut, intent.Context);
    }

    // ── AT-09b: Work overrides Idle ───────────────────────────────────────────

    [Fact]
    public void ActiveTask_AtDesk_QuietDrives_WorkBeatsIdle()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        var task = em.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent { Priority = 50, Progress = 0.0f });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new WillpowerComponent(50, 50));
        npc.Add(new SocialDrivesComponent()); // all drives quiet
        npc.Add(new WorkloadComponent { ActiveTasks = new List<Guid> { task.Id }, Capacity = 3 });
        npc.Add(new CurrentScheduleBlockComponent
        {
            ActiveBlockIndex = 0,
            AnchorEntityId   = Guid.Empty,
            Activity         = ScheduleActivityKind.AtDesk
        });
        spatial.Register(npc, 5, 5);

        var sys = new ActionSelectionSystem(spatial, new EntityRoomMembership(), queue,
            new SeededRandom(42), DefaultCfg(), SchedCfg(), em,
            new WorkloadConfig { WorkActionBaseWeight = 0.40 }); // 0.40 > IdleFloor 0.20

        sys.Update(em, 1f);

        var intent = npc.Get<IntendedActionComponent>();
        // Work (0.40) > Idle (0.20) — Work should win
        Assert.Equal(IntendedActionKind.Work, intent.Kind);
        Assert.NotEqual(IntendedActionKind.Idle, intent.Kind);
    }

    [Fact]
    public void NoTasks_AtDesk_ProducesIdleNotWork()
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

        // Without active tasks, only Idle candidate present — Idle wins
        Assert.Equal(IntendedActionKind.Idle, npc.Get<IntendedActionComponent>().Kind);
    }
}
