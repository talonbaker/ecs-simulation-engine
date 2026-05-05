using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Integration;

/// <summary>
/// AT-06: NPC with quiet drives + schedule pointing at an anchor produces Approach to that anchor.
/// AT-07: Same NPC with Irritation=80 and a coworker nearby produces Dialog/LashOut — drive overrides schedule.
/// AT-08: NPC already at its schedule anchor (within ScheduleLingerThresholdCells) and AtDesk produces Linger.
/// </summary>
public class ActionSelectionScheduleIntegrationTests
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

    // -- AT-06 -----------------------------------------------------------------

    [Fact]
    public void AT06_QuietDrives_ScheduleWins_ProducesApproachToAnchor()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        // Anchor far away → atAnchor = false → Approach
        var anchor = em.CreateEntity();
        anchor.Add(new NamedAnchorComponent { Tag = "the-microwave" });
        anchor.Add(new PositionComponent { X = 20f, Y = 0f, Z = 20f });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new WillpowerComponent(50, 50));
        npc.Add(new SocialDrivesComponent()); // all drives at 0 — no drive candidates
        npc.Add(new CurrentScheduleBlockComponent
        {
            ActiveBlockIndex = 0,
            AnchorEntityId   = anchor.Id,
            Activity         = ScheduleActivityKind.Lunch
        });
        spatial.Register(npc, 5, 5);

        var sys = new ActionSelectionSystem(spatial, new EntityRoomMembership(), queue,
            new SeededRandom(42), DefaultCfg(), SchedCfg(), em);
        sys.Update(em, 1f);

        Assert.True(npc.Has<IntendedActionComponent>());
        var intent = npc.Get<IntendedActionComponent>();
        Assert.Equal(IntendedActionKind.Approach, intent.Kind);
        Assert.Equal(WillpowerSystem.EntityIntId(anchor), intent.TargetEntityId);
    }

    // -- AT-07 -----------------------------------------------------------------

    [Fact]
    public void AT07_ElevatedIrritation_DriveOverridesSchedule_ProducesDialogLashOut()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        var anchor = em.CreateEntity();
        anchor.Add(new NamedAnchorComponent { Tag = "the-microwave" });
        anchor.Add(new PositionComponent { X = 20f, Y = 0f, Z = 20f });

        // Coworker 1 tile away so Dialog/LashOut (NeedsTarget=true) is enumerated
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
        // No Confrontation inhibition → LashOut candidate gets full weight (~0.944) vs schedule 0.30
        npc.Add(new CurrentScheduleBlockComponent
        {
            ActiveBlockIndex = 0,
            AnchorEntityId   = anchor.Id,
            Activity         = ScheduleActivityKind.Lunch
        });
        spatial.Register(npc, 5, 5);

        var sys = new ActionSelectionSystem(spatial, new EntityRoomMembership(), queue,
            new SeededRandom(42), DefaultCfg(), SchedCfg(), em);
        sys.Update(em, 1f);

        Assert.True(npc.Has<IntendedActionComponent>());
        var intent = npc.Get<IntendedActionComponent>();
        Assert.Equal(IntendedActionKind.Dialog,          intent.Kind);
        Assert.Equal(DialogContextValue.LashOut,         intent.Context);
    }

    // -- AT-08 -----------------------------------------------------------------

    [Fact]
    public void AT08_NpcAtAnchor_AtDesk_ProducesLinger()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        // Anchor at same tile as NPC → distance = 0 < ScheduleLingerThresholdCells (2.0)
        var anchor = em.CreateEntity();
        anchor.Add(new NamedAnchorComponent { Tag = "the-window" });
        anchor.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new WillpowerComponent(50, 50));
        npc.Add(new SocialDrivesComponent()); // quiet drives
        npc.Add(new CurrentScheduleBlockComponent
        {
            ActiveBlockIndex = 0,
            AnchorEntityId   = anchor.Id,
            Activity         = ScheduleActivityKind.AtDesk
        });
        spatial.Register(npc, 5, 5);

        var sys = new ActionSelectionSystem(spatial, new EntityRoomMembership(), queue,
            new SeededRandom(42), DefaultCfg(), SchedCfg(), em);
        sys.Update(em, 1f);

        Assert.True(npc.Has<IntendedActionComponent>());
        var intent = npc.Get<IntendedActionComponent>();
        Assert.Equal(IntendedActionKind.Linger, intent.Kind);
    }
}
