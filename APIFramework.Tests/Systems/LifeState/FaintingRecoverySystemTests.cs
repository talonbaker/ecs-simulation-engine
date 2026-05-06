using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.LifeState;

/// <summary>
/// AT-10: NPC with IsFaintingTag and RecoveryTick == currentTick → recovery queued
///        → NPC becomes Alive after LifeStateTransitions.Update.
/// AT-11: NPC with RecoveryTick > currentTick → no recovery queued (still out).
/// AT-12: EmitRegainedConsciousnessNarrative=true → RegainedConsciousness candidate emitted.
/// </summary>
public class FaintingRecoverySystemTests
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

    /// <summary>
    /// Builds a fainted NPC already in Incapacitated state with IsFaintingTag and
    /// FaintingComponent. RecoveryTick can be set to currentTick (due now) or future.
    /// </summary>
    private static (
        EntityManager em,
        NarrativeEventBus bus,
        SimulationClock clock,
        LifeStateTransitionSystem transitions,
        Entity npc)
    Build(long recoveryTickOffset = 0) // 0 = recovery is due this tick; positive = future
    {
        var em          = new EntityManager();
        var bus         = new NarrativeEventBus();
        var clock       = new SimulationClock();
        var config      = new SimConfig { LifeState = DefaultLifeStateCfg() };
        var transitions = new LifeStateTransitionSystem(bus, em, clock, config);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent
        {
            State                   = LS.Incapacitated,
            IncapacitatedTickBudget = 21, // FaintDurationTicks+1 — won't expire before recovery
            PendingDeathCause       = CauseOfDeath.Unknown,
        });
        npc.Add(new IsFaintingTag());
        npc.Add(new FaintingComponent
        {
            FaintStartTick = clock.CurrentTick - 20,
            RecoveryTick   = clock.CurrentTick + recoveryTickOffset,
        });

        return (em, bus, clock, transitions, npc);
    }

    private static FaintingRecoverySystem MakeSys(
        LifeStateTransitionSystem transitions,
        NarrativeEventBus bus,
        SimulationClock clock,
        FaintingConfig? cfg = null)
        => new(transitions, bus, clock, cfg ?? DefaultCfg());

    private static List<NarrativeEventCandidate> Collect(NarrativeEventBus bus, Action tick)
    {
        var list = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += list.Add;
        tick();
        bus.OnCandidateEmitted -= list.Add;
        return list;
    }

    // -- AT-10: Recovery due → NPC becomes Alive -------------------------------

    [Fact]
    public void AT10_RecoveryTickReached_NpcBecomesAlive()
    {
        var (em, bus, clock, transitions, npc) = Build(recoveryTickOffset: 0);

        MakeSys(transitions, bus, clock).Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.Equal(LS.Alive, npc.Get<LifeStateComponent>().State);
    }

    [Fact]
    public void AT10_RecoveryTickInPast_NpcAlsoBecomesAlive()
    {
        // RecoveryTick already passed (currentTick > recoveryTick); should still recover.
        var (em, bus, clock, transitions, npc) = Build(recoveryTickOffset: -5);

        MakeSys(transitions, bus, clock).Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.Equal(LS.Alive, npc.Get<LifeStateComponent>().State);
    }

    // -- AT-11: Recovery not yet due → NPC stays Incapacitated ----------------

    [Fact]
    public void AT11_RecoveryTickInFuture_NpcRemainsIncapacitated()
    {
        var (em, bus, clock, transitions, npc) = Build(recoveryTickOffset: 5);

        MakeSys(transitions, bus, clock).Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.Equal(LS.Incapacitated, npc.Get<LifeStateComponent>().State);
    }

    // -- AT-12: RegainedConsciousness narrative emitted ------------------------

    [Fact]
    public void AT12_EmitNarrativeTrue_RegainedConsciousnessCandidateEmitted()
    {
        var (em, bus, clock, transitions, _) = Build(recoveryTickOffset: 0);
        var cfg = DefaultCfg();
        cfg.EmitRegainedConsciousnessNarrative = true;

        var candidates = Collect(bus, () =>
            MakeSys(transitions, bus, clock, cfg).Update(em, 1f));

        Assert.Contains(candidates, c => c.Kind == NarrativeEventKind.RegainedConsciousness);
    }

    [Fact]
    public void AT12_EmitNarrativeFalse_NoRegainedConsciousnessCandidate()
    {
        var (em, bus, clock, transitions, _) = Build(recoveryTickOffset: 0);
        var cfg = DefaultCfg();
        cfg.EmitRegainedConsciousnessNarrative = false;

        var candidates = Collect(bus, () =>
            MakeSys(transitions, bus, clock, cfg).Update(em, 1f));

        Assert.DoesNotContain(candidates, c => c.Kind == NarrativeEventKind.RegainedConsciousness);
    }
}
