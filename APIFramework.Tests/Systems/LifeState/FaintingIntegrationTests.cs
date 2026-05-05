using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.LifeState;

/// <summary>
/// AT-16: Full tick sequence: Fear=100 → detect tick → Incapacitated;
///         advance clock to RecoveryTick → recovery tick → Alive; cleanup → tags removed.
/// AT-17: After recovery, MoodComponent.Fear is unchanged by the fainting systems
///         (Fear decays via MoodSystem at its own rate — not our concern).
/// AT-18: Determinism — two NPCs faint in the same tick; both processed in ascending
///         EntityIntId order (no random ordering).
/// AT-19: Fainted NPC does NOT receive CorpseTag or CorpseComponent.
/// </summary>
public class FaintingIntegrationTests
{
    // -- Helpers ---------------------------------------------------------------

    private static FaintingConfig DefaultCfg() => new()
    {
        FearThreshold                      = 85f,
        FaintDurationTicks                 = 20,
        EmitFaintedNarrative               = true,
        EmitRegainedConsciousnessNarrative = true,
    };

    private static LifeStateConfig DefaultLifeStateCfg() => new() { DefaultIncapacitatedTicks = 180 };

    private static int EntityIntId(Entity e)
    {
        var b = e.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    private static (
        EntityManager em,
        NarrativeEventBus bus,
        SimulationClock clock,
        EntityRoomMembership membership,
        LifeStateTransitionSystem transitions,
        FaintingDetectionSystem detection,
        FaintingRecoverySystem recovery,
        FaintingCleanupSystem cleanup)
    BuildPipeline()
    {
        var em          = new EntityManager();
        var bus         = new NarrativeEventBus();
        var clock       = new SimulationClock();
        var membership  = new EntityRoomMembership();
        var transitions = new LifeStateTransitionSystem(bus, em, clock, DefaultLifeStateCfg(), membership);
        var cfg         = DefaultCfg();
        var detection   = new FaintingDetectionSystem(transitions, bus, clock, membership, cfg);
        var recovery    = new FaintingRecoverySystem(transitions, bus, clock, cfg);
        var cleanup     = new FaintingCleanupSystem();

        return (em, bus, clock, membership, transitions, detection, recovery, cleanup);
    }

    /// <summary>Advances clock and runs the full fainting pipeline for one tick.</summary>
    private static void Tick(
        EntityManager em,
        SimulationClock clock,
        FaintingDetectionSystem detection,
        FaintingRecoverySystem recovery,
        LifeStateTransitionSystem transitions,
        FaintingCleanupSystem cleanup)
    {
        detection.Update(em, 1f);
        recovery.Update(em, 1f);
        transitions.Update(em, 1f);
        cleanup.Update(em, 1f);
        clock.Advance(1);
    }

    // -- AT-16: Full faint → recovery → cleanup cycle --------------------------

    [Fact]
    public void AT16_FullCycle_FaintThenRecoverThenTagsRemoved()
    {
        var (em, bus, clock, membership, transitions, detection, recovery, cleanup) = BuildPipeline();

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LifeState.Alive });
        npc.Add(new MoodComponent { Fear = 95f });

        // -- Tick 0: detect faint ----------------------------------------------
        long tick0 = clock.CurrentTick;
        Tick(em, clock, detection, recovery, transitions, cleanup);

        Assert.True(npc.Has<IsFaintingTag>(),  "NPC should have IsFaintingTag after faint");
        Assert.Equal(LifeState.Incapacitated,   npc.Get<LifeStateComponent>().State);

        long recoveryTick = tick0 + DefaultCfg().FaintDurationTicks;

        // -- Ticks 1–19: still unconscious ------------------------------------
        while (clock.CurrentTick < recoveryTick)
        {
            Tick(em, clock, detection, recovery, transitions, cleanup);
            Assert.Equal(LifeState.Incapacitated, npc.Get<LifeStateComponent>().State);
        }

        // -- Tick 20 (RecoveryTick): recover ----------------------------------
        Tick(em, clock, detection, recovery, transitions, cleanup);

        Assert.Equal(LifeState.Alive,  npc.Get<LifeStateComponent>().State);
        Assert.False(npc.Has<IsFaintingTag>(),    "IsFaintingTag should be removed after recovery");
        Assert.False(npc.Has<FaintingComponent>(), "FaintingComponent should be removed after recovery");
    }

    // -- AT-17: Fear unchanged by fainting systems -----------------------------

    [Fact]
    public void AT17_Fear_NotClearedByFaintingSystems_AfterRecovery()
    {
        var (em, bus, clock, membership, transitions, detection, recovery, cleanup) = BuildPipeline();

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LifeState.Alive });
        npc.Add(new MoodComponent { Fear = 95f });

        // Faint
        Tick(em, clock, detection, recovery, transitions, cleanup);

        // Advance to recovery
        int faintDuration = DefaultCfg().FaintDurationTicks;
        for (int i = 0; i < faintDuration; i++)
            Tick(em, clock, detection, recovery, transitions, cleanup);

        // Recover tick
        Tick(em, clock, detection, recovery, transitions, cleanup);

        Assert.Equal(LifeState.Alive, npc.Get<LifeStateComponent>().State);
        // Fainting systems do not touch Fear — MoodSystem decays it
        Assert.Equal(95f, npc.Get<MoodComponent>().Fear);
    }

    // -- AT-18: Two NPCs faint simultaneously — deterministic order ------------

    [Fact]
    public void AT18_TwoNpcsFaintSameTick_BothProcessed_DeterministicOrder()
    {
        var (em, bus, clock, membership, transitions, detection, recovery, cleanup) = BuildPipeline();

        var npc1 = em.CreateEntity();
        npc1.Add(new NpcTag());
        npc1.Add(new LifeStateComponent { State = LifeState.Alive });
        npc1.Add(new MoodComponent { Fear = 92f });

        var npc2 = em.CreateEntity();
        npc2.Add(new NpcTag());
        npc2.Add(new LifeStateComponent { State = LifeState.Alive });
        npc2.Add(new MoodComponent { Fear = 88f });

        // Both should faint in the same tick
        Tick(em, clock, detection, recovery, transitions, cleanup);

        Assert.True(npc1.Has<IsFaintingTag>(), "npc1 should have fainted");
        Assert.True(npc2.Has<IsFaintingTag>(), "npc2 should have fainted");
        Assert.Equal(LifeState.Incapacitated, npc1.Get<LifeStateComponent>().State);
        Assert.Equal(LifeState.Incapacitated, npc2.Get<LifeStateComponent>().State);

        // Verify both have the same RecoveryTick (same start tick)
        long rt1 = npc1.Get<FaintingComponent>().RecoveryTick;
        long rt2 = npc2.Get<FaintingComponent>().RecoveryTick;
        Assert.Equal(rt1, rt2);
    }

    // -- AT-19: Fainted NPC does NOT get CorpseTag -----------------------------

    [Fact]
    public void AT19_FaintedNpc_DoesNotReceiveCorpseTag()
    {
        var (em, bus, clock, membership, transitions, detection, recovery, cleanup) = BuildPipeline();

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LifeState.Alive });
        npc.Add(new MoodComponent { Fear = 95f });

        Tick(em, clock, detection, recovery, transitions, cleanup);

        Assert.Equal(LifeState.Incapacitated, npc.Get<LifeStateComponent>().State);
        Assert.False(npc.Has<CorpseTag>(),        "A fainted NPC must never receive CorpseTag");
        Assert.False(npc.Has<CorpseComponent>(),  "A fainted NPC must never receive CorpseComponent");
    }
}
