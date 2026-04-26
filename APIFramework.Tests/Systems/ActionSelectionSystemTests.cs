using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// Unit tests for ActionSelectionSystem: AT-01 through AT-06.
/// Uses seeded RNG and synthetic NPC + spatial scaffolding.
/// </summary>
public class ActionSelectionSystemTests
{
    // ── Scaffolding ───────────────────────────────────────────────────────────

    private static ActionSelectionConfig DefaultCfg() => new()
    {
        DriveCandidateThreshold      = 60,
        IdleScoreFloor               = 0.20,
        InversionStakeThreshold      = 0.55,
        InversionInhibitionThreshold = 0.50,
        SuppressionGiveUpFactor      = 0.30,
        SuppressionEpsilon           = 0.10,
        SuppressionEventMagnitudeScale = 5,
        PersonalityTieBreakWeight    = 0.05,
        MaxCandidatesPerTick         = 32,
        AvoidStandoffDistance        = 4
    };

    private static GridSpatialIndex MakeSpatial() =>
        new(new SpatialConfig { CellSizeTiles = 4, WorldSize = new() { Width = 64, Height = 64 } });

    private static ActionSelectionSystem MakeSystem(
        EntityManager        em,
        ISpatialIndex        spatial,
        WillpowerEventQueue  queue,
        ActionSelectionConfig? cfg = null,
        int seed = 42) =>
        new(spatial, new EntityRoomMembership(), queue, new SeededRandom(seed),
            cfg ?? DefaultCfg(), new ScheduleConfig(), em);

    /// <summary>Creates an NPC with SocialDrivesComponent and places it at (5, 5).</summary>
    private static Entity SpawnNpc(EntityManager em, ISpatialIndex spatial,
                                   Action<Entity>? configure = null)
    {
        var e = em.CreateEntity();
        e.Add(new NpcTag());
        e.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        e.Add(new SocialDrivesComponent());
        e.Add(new WillpowerComponent(50, 50));
        e.Add(new InhibitionsComponent(Array.Empty<Inhibition>()));
        configure?.Invoke(e);
        spatial.Register(e, 5, 5);
        return e;
    }

    // ── AT-01: Component round-trips ──────────────────────────────────────────

    [Fact]
    public void AT01_IntendedActionComponent_InstantiatesAndRoundTrips()
    {
        var idle    = new IntendedActionComponent(IntendedActionKind.Idle, 0, DialogContextValue.None, 0);
        var dialog  = new IntendedActionComponent(IntendedActionKind.Dialog, 7, DialogContextValue.LashOut, 80);
        var approach = new IntendedActionComponent(IntendedActionKind.Approach, 3, DialogContextValue.None, 60);

        Assert.Equal(IntendedActionKind.Idle,    idle.Kind);
        Assert.Equal(0,                          idle.TargetEntityId);
        Assert.Equal(DialogContextValue.None,    idle.Context);

        Assert.Equal(IntendedActionKind.Dialog,  dialog.Kind);
        Assert.Equal(7,                          dialog.TargetEntityId);
        Assert.Equal(DialogContextValue.LashOut, dialog.Context);
        Assert.Equal(80,                         dialog.IntensityHint);

        // Record equality
        Assert.Equal(dialog, new IntendedActionComponent(IntendedActionKind.Dialog, 7, DialogContextValue.LashOut, 80));
        Assert.NotEqual(dialog, approach);
    }

    [Fact]
    public void AT01_AllEnumValuesPresent()
    {
        // IntendedActionKind
        _ = IntendedActionKind.Idle;
        _ = IntendedActionKind.Dialog;
        _ = IntendedActionKind.Approach;
        _ = IntendedActionKind.Avoid;
        _ = IntendedActionKind.Linger;

        // DialogContextValue
        _ = DialogContextValue.None;
        _ = DialogContextValue.LashOut;
        _ = DialogContextValue.Share;
        _ = DialogContextValue.Flirt;
        _ = DialogContextValue.Deflect;
        _ = DialogContextValue.BrushOff;
        _ = DialogContextValue.Acknowledge;
        _ = DialogContextValue.Greet;
        _ = DialogContextValue.Refuse;
        _ = DialogContextValue.Agree;
        _ = DialogContextValue.Complain;
        _ = DialogContextValue.Encourage;
        _ = DialogContextValue.Thanks;
        _ = DialogContextValue.Apologise;
    }

    // ── AT-02: Irritation → Dialog within 50 ticks ───────────────────────────

    [Fact]
    public void AT02_HighIrritation_EmitsLashOutOrComplain_Within50Ticks()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        var npc = SpawnNpc(em, spatial, e =>
        {
            e.Add(new SocialDrivesComponent { Irritation = new DriveValue { Current = 80, Baseline = 80 } });
            e.Add(new WillpowerComponent(50, 50));
        });

        // Coworker in proximity
        var coworker = em.CreateEntity();
        coworker.Add(new NpcTag());
        coworker.Add(new PositionComponent { X = 6f, Y = 0f, Z = 5f }); // 1 tile away
        spatial.Register(coworker, 6, 5);

        var sys = MakeSystem(em, spatial, queue);

        bool emitted = false;
        for (int t = 0; t < 50 && !emitted; t++)
        {
            sys.Update(em, 1f);
            if (npc.Has<IntendedActionComponent>())
            {
                var intent = npc.Get<IntendedActionComponent>();
                if (intent.Kind == IntendedActionKind.Dialog &&
                    (intent.Context == DialogContextValue.LashOut ||
                     intent.Context == DialogContextValue.Complain))
                    emitted = true;
            }
        }

        Assert.True(emitted, "Expected LashOut or Complain dialog within 50 ticks");
    }

    // ── AT-03: Low Vulnerability → Approach ──────────────────────────────────

    [Fact]
    public void AT03_HighAttraction_LowVulnerability_EmitsApproach()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        var target = em.CreateEntity();
        target.Add(new NpcTag());
        target.Add(new PositionComponent { X = 6f, Y = 0f, Z = 5f });
        spatial.Register(target, 6, 5);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new WillpowerComponent(60, 60));
        npc.Add(new SocialDrivesComponent { Attraction = new DriveValue { Current = 80, Baseline = 80 } });
        npc.Add(new InhibitionsComponent(new[]
        {
            new Inhibition(InhibitionClass.Vulnerability, 20, InhibitionAwareness.Known)
        }));
        spatial.Register(npc, 5, 5);

        var sys = MakeSystem(em, spatial, queue);
        sys.Update(em, 1f);

        Assert.True(npc.Has<IntendedActionComponent>());
        var intent = npc.Get<IntendedActionComponent>();
        Assert.Equal(IntendedActionKind.Approach, intent.Kind);
    }

    // ── AT-06: All drives below threshold → Idle ─────────────────────────────

    [Fact]
    public void AT06_AllDrivesBelowThreshold_EmitsIdle()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        var npc = SpawnNpc(em, spatial, e =>
        {
            // All drives at 50, below the default threshold of 60
            e.Add(new SocialDrivesComponent
            {
                Belonging  = new DriveValue { Current = 50 },
                Status     = new DriveValue { Current = 50 },
                Affection  = new DriveValue { Current = 50 },
                Irritation = new DriveValue { Current = 50 },
                Attraction = new DriveValue { Current = 50 },
                Trust      = new DriveValue { Current = 50 },
                Suspicion  = new DriveValue { Current = 50 },
                Loneliness = new DriveValue { Current = 50 },
            });
        });

        var sys = MakeSystem(em, spatial, queue);
        sys.Update(em, 1f);

        Assert.True(npc.Has<IntendedActionComponent>());
        Assert.Equal(IntendedActionKind.Idle, npc.Get<IntendedActionComponent>().Kind);
    }

    [Fact]
    public void AT06_NoDriveComponent_EmitsNoIntent()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        // No SocialDrivesComponent
        spatial.Register(npc, 5, 5);

        var sys = MakeSystem(em, spatial, queue);
        sys.Update(em, 1f);

        // NPC without SocialDrivesComponent is skipped entirely
        Assert.False(npc.Has<IntendedActionComponent>());
    }

    // ── Intent overwrite each tick ────────────────────────────────────────────

    [Fact]
    public void IntentIsOverwrittenEachTick()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        var npc = SpawnNpc(em, spatial, e =>
        {
            e.Add(new SocialDrivesComponent { Irritation = new DriveValue { Current = 80 } });
            e.Add(new WillpowerComponent(50, 50));
        });

        var coworker = em.CreateEntity();
        coworker.Add(new NpcTag());
        coworker.Add(new PositionComponent { X = 6f, Y = 0f, Z = 5f });
        spatial.Register(coworker, 6, 5);

        var sys = MakeSystem(em, spatial, queue);

        // Run two ticks; intent should be present after each
        sys.Update(em, 1f);
        Assert.True(npc.Has<IntendedActionComponent>());

        sys.Update(em, 1f);
        Assert.True(npc.Has<IntendedActionComponent>());
    }
}
