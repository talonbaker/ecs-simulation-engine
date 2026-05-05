using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.LifeState;

/// <summary>
/// AT-01: Fear >= FearThreshold (Alive NPC) → IsFaintingTag attached.
/// AT-02: Fear >= FearThreshold → FaintingComponent.RecoveryTick = currentTick + FaintDurationTicks.
/// AT-03: Fear &lt; FearThreshold → no faint, no tag.
/// AT-04: NPC already has IsFaintingTag → idempotent; FaintingComponent not overwritten.
/// AT-05: Deceased NPC with Fear=100 → not triggered (LifeStateGuard).
/// AT-06: Incapacitated NPC with Fear=100 → not triggered (not Alive).
/// AT-07: EmitFaintedNarrative=true → Fainted candidate on NarrativeEventBus.
/// AT-08: EmitFaintedNarrative=false → no Fainted candidate.
/// AT-09: After FaintingDetectionSystem.Update + LifeStateTransitions.Update → NPC is Incapacitated.
/// </summary>
public class FaintingDetectionSystemTests
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

    private static (
        EntityManager em,
        NarrativeEventBus bus,
        SimulationClock clock,
        EntityRoomMembership membership,
        LifeStateTransitionSystem transitions,
        Entity npc)
    Build(
        float fear      = 90f,           // above threshold by default
        LifeState state = LifeState.Alive,
        bool  alreadyFainting = false)
    {
        var em         = new EntityManager();
        var bus        = new NarrativeEventBus();
        var clock      = new SimulationClock();
        var membership = new EntityRoomMembership();
        var transitions = new LifeStateTransitionSystem(bus, em, clock, DefaultLifeStateCfg(), membership);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = state });
        npc.Add(new MoodComponent { Fear = fear });
        npc.Add(new PositionComponent { X = 0, Z = 0 });
        npc.Add(new ProximityComponent());
        if (alreadyFainting)
        {
            npc.Add(new IsFaintingTag());
            npc.Add(new FaintingComponent { FaintStartTick = 1, RecoveryTick = 21 });
        }

        return (em, bus, clock, membership, transitions, npc);
    }

    private static FaintingDetectionSystem MakeSys(
        LifeStateTransitionSystem transitions,
        NarrativeEventBus bus,
        SimulationClock clock,
        EntityRoomMembership membership,
        FaintingConfig? cfg = null)
        => new(transitions, bus, clock, membership, cfg ?? DefaultCfg());

    private static List<NarrativeEventCandidate> Collect(NarrativeEventBus bus, Action tick)
    {
        var list = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += list.Add;
        tick();
        bus.OnCandidateEmitted -= list.Add;
        return list;
    }

    // -- AT-01: Fear >= threshold → IsFaintingTag ------------------------------

    [Fact]
    public void AT01_FearAboveThreshold_AliveNpc_AttachesIsFaintingTag()
    {
        var (em, bus, clock, membership, transitions, npc) = Build(fear: 90f);
        MakeSys(transitions, bus, clock, membership).Update(em, 1f);

        Assert.True(npc.Has<IsFaintingTag>());
    }

    // -- AT-02: FaintingComponent.RecoveryTick is correct ---------------------

    [Fact]
    public void AT02_FaintingComponent_RecoveryTick_EqualsStartPlusDuration()
    {
        var (em, bus, clock, membership, transitions, npc) = Build(fear: 90f);
        long startTick = clock.CurrentTick;
        MakeSys(transitions, bus, clock, membership).Update(em, 1f);

        Assert.True(npc.Has<FaintingComponent>());
        var fc = npc.Get<FaintingComponent>();
        Assert.Equal(startTick, fc.FaintStartTick);
        Assert.Equal(startTick + DefaultCfg().FaintDurationTicks, fc.RecoveryTick);
    }

    // -- AT-03: Fear below threshold → no faint -------------------------------

    [Fact]
    public void AT03_FearBelowThreshold_NoFaint()
    {
        var (em, bus, clock, membership, transitions, npc) = Build(fear: 50f);
        MakeSys(transitions, bus, clock, membership).Update(em, 1f);

        Assert.False(npc.Has<IsFaintingTag>());
    }

    [Fact]
    public void AT03_FearExactlyAtThreshold_TriggersFaint()
    {
        // Threshold check is >= so exactly 85 triggers.
        var (em, bus, clock, membership, transitions, npc) = Build(fear: 85f);
        MakeSys(transitions, bus, clock, membership).Update(em, 1f);

        Assert.True(npc.Has<IsFaintingTag>());
    }

    // -- AT-04: Already fainting → idempotent ---------------------------------

    [Fact]
    public void AT04_AlreadyFainting_Idempotent_FaintingComponentNotOverwritten()
    {
        var (em, bus, clock, membership, transitions, npc) = Build(alreadyFainting: true);
        // RecoveryTick was set to 21 in Build helper
        long originalRecoveryTick = npc.Get<FaintingComponent>().RecoveryTick;

        MakeSys(transitions, bus, clock, membership).Update(em, 1f);

        // FaintingComponent must not be overwritten
        Assert.Equal(originalRecoveryTick, npc.Get<FaintingComponent>().RecoveryTick);
    }

    // -- AT-05: Deceased NPC → not triggered ----------------------------------

    [Fact]
    public void AT05_DeceasedNpc_Fear100_NoFaint()
    {
        var (em, bus, clock, membership, transitions, npc) = Build(fear: 100f, state: LifeState.Deceased);
        MakeSys(transitions, bus, clock, membership).Update(em, 1f);

        Assert.False(npc.Has<IsFaintingTag>());
    }

    // -- AT-06: Incapacitated NPC → not triggered ------------------------------

    [Fact]
    public void AT06_IncapacitatedNpc_Fear100_NoFaint()
    {
        var (em, bus, clock, membership, transitions, npc) = Build(fear: 100f, state: LifeState.Incapacitated);
        MakeSys(transitions, bus, clock, membership).Update(em, 1f);

        Assert.False(npc.Has<IsFaintingTag>());
    }

    // -- AT-07: Fainted narrative emitted -------------------------------------

    [Fact]
    public void AT07_EmitNarrativeTrue_FaintedCandidateEmitted()
    {
        var (em, bus, clock, membership, transitions, _) = Build(fear: 90f);
        var cfg = DefaultCfg();
        cfg.EmitFaintedNarrative = true;

        var candidates = Collect(bus, () =>
            MakeSys(transitions, bus, clock, membership, cfg).Update(em, 1f));

        Assert.Contains(candidates, c => c.Kind == NarrativeEventKind.Fainted);
    }

    // -- AT-08: No narrative when flag is off ---------------------------------

    [Fact]
    public void AT08_EmitNarrativeFalse_NoFaintedCandidate()
    {
        var (em, bus, clock, membership, transitions, _) = Build(fear: 90f);
        var cfg = DefaultCfg();
        cfg.EmitFaintedNarrative = false;

        var candidates = Collect(bus, () =>
            MakeSys(transitions, bus, clock, membership, cfg).Update(em, 1f));

        Assert.DoesNotContain(candidates, c => c.Kind == NarrativeEventKind.Fainted);
    }

    // -- AT-09: NPC becomes Incapacitated after drain -------------------------

    [Fact]
    public void AT09_AfterTransitionsDrain_NpcIsIncapacitated()
    {
        var (em, bus, clock, membership, transitions, npc) = Build(fear: 90f);

        MakeSys(transitions, bus, clock, membership).Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.Equal(LifeState.Incapacitated, npc.Get<LifeStateComponent>().State);
    }
}
