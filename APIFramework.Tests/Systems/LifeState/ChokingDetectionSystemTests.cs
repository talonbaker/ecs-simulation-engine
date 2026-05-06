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
/// AT-01: Tough bolus + distracted NPC → IsChokingTag attached, incapacitation queued.
/// AT-02: Soft bolus (Toughness below threshold) → no choke triggered.
/// AT-03: NPC already has IsChokingTag → idempotent; no second trigger.
/// AT-04: NPC is Deceased → choking not triggered.
/// AT-05: No distraction conditions met → choke not triggered.
/// AT-06: Energy below EnergyThreshold alone → distraction satisfied.
/// AT-07: AcuteLevel >= StressThreshold alone → distraction satisfied.
/// AT-08: Irritation.Current >= IrritationThreshold alone → distraction satisfied.
/// AT-09: ChokeStarted narrative emitted when EmitChokeStartedNarrative = true.
/// AT-10: No ChokeStarted narrative emitted when EmitChokeStartedNarrative = false.
/// AT-11: ChokingComponent.BolusSize mirrors bolus toughness on detection.
/// AT-12: MoodComponent.PanicLevel is set to PanicMoodIntensity on detection.
/// AT-13: Incapacitation request is drained by LifeStateTransitionSystem in same tick.
/// </summary>
public class ChokingDetectionSystemTests
{
    // -- Helpers ---------------------------------------------------------------

    private static ChokingConfig DefaultCfg() => new()
    {
        BolusSizeThreshold        = 0.65f,
        EnergyThreshold           = 40,
        StressThreshold           = 70,
        IrritationThreshold       = 65,
        IncapacitationTicks       = 90,
        PanicMoodIntensity        = 0.85f,
        EmitChokeStartedNarrative = true,
    };

    private static LifeStateConfig DefaultLifeStateCfg() => new() { DefaultIncapacitatedTicks = 180 };

    /// <summary>
    /// Builds a minimal world: one bolus entity (food in esophageal transit) and one NPC.
    /// Returns all the pieces needed to invoke ChokingDetectionSystem.
    /// </summary>
    private static (
        EntityManager em,
        NarrativeEventBus bus,
        SimulationClock clock,
        EntityRoomMembership membership,
        LifeStateTransitionSystem transitions,
        Entity bolus,
        Entity npc)
    Build(
        float bolusThoughness         = 0.75f,   // above threshold by default
        float energy                   = 30f,    // below EnergyThreshold → distracted
        int   acuteLevel               = 0,
        int   irritationCurrent        = 0,
        LS npcState             = LS.Alive,
        bool  alreadyChoking           = false)
    {
        var em         = new EntityManager();
        var bus        = new NarrativeEventBus();
        var clock      = new SimulationClock();
        var membership = new EntityRoomMembership();
        var config     = new SimConfig { LifeState = DefaultLifeStateCfg() };

        var transitions = new LifeStateTransitionSystem(bus, em, clock, config);

        // Room
        var room = em.CreateEntity();
        room.Add(new RoomComponent { Id = "r1", Name = "breakroom" });

        // NPC
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = npcState });
        npc.Add(new EnergyComponent { Energy = energy });
        npc.Add(new StressComponent { AcuteLevel = acuteLevel });
        npc.Add(new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = irritationCurrent, Baseline = 0 }
        });
        npc.Add(new MoodComponent());
        npc.Add(new PositionComponent { X = 0, Z = 0 });
        npc.Add(new ProximityComponent());
        if (alreadyChoking) npc.Add(new IsChokingTag());
        membership.SetRoom(npc, room);

        // Bolus entity (food in transit)
        var bolus = em.CreateEntity();
        bolus.Add(new BolusComponent
        {
            FoodType  = "TestFood",
            Toughness = bolusThoughness,
            Volume    = 100f,
        });
        bolus.Add(new EsophagusTransitComponent
        {
            TargetEntityId = npc.Id,
            Progress       = 0.5f,
            Speed          = 1f,
        });

        return (em, bus, clock, membership, transitions, bolus, npc);
    }

    private static List<NarrativeEventCandidate> Collect(NarrativeEventBus bus, Action tick)
    {
        var list = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += list.Add;
        tick();
        bus.OnCandidateEmitted -= list.Add;
        return list;
    }

    private static ChokingDetectionSystem MakeSys(
        LifeStateTransitionSystem transitions,
        NarrativeEventBus bus,
        SimulationClock clock,
        EntityManager em,
        ChokingConfig? cfg = null)
        => new(transitions, bus, clock, cfg ?? DefaultCfg(), em);

    // -- AT-01: Tough bolus + distracted NPC → IsChokingTag -------------------

    [Fact]
    public void AT01_ToughBolus_DistractedNpc_AttachesIsChokingTag()
    {
        var (em, bus, clock, membership, transitions, _, npc) = Build();
        MakeSys(transitions, bus, clock, em).Update(em, 1f);

        Assert.True(npc.Has<IsChokingTag>());
    }

    [Fact]
    public void AT01_ToughBolus_DistractedNpc_AttachesChokingComponent()
    {
        var (em, bus, clock, membership, transitions, _, npc) = Build();
        MakeSys(transitions, bus, clock, em).Update(em, 1f);

        Assert.True(npc.Has<ChokingComponent>());
    }

    // -- AT-02: Soft bolus → no choke -----------------------------------------

    [Fact]
    public void AT02_SoftBolus_BelowThreshold_NoIsChokingTag()
    {
        var (em, bus, clock, membership, transitions, _, npc) = Build(bolusThoughness: 0.40f);
        MakeSys(transitions, bus, clock, em).Update(em, 1f);

        Assert.False(npc.Has<IsChokingTag>());
    }

    [Fact]
    public void AT02_BolusToughnessExactlyAtThreshold_TriggersChoke()
    {
        // threshold is < (strictly less-than check), so exactly 0.65 still triggers
        var (em, bus, clock, membership, transitions, _, npc) = Build(bolusThoughness: 0.65f);
        MakeSys(transitions, bus, clock, em).Update(em, 1f);

        Assert.True(npc.Has<IsChokingTag>());
    }

    // -- AT-03: Already choking → idempotent ----------------------------------

    [Fact]
    public void AT03_NpcAlreadyChoking_NoSecondChokingComponent()
    {
        var (em, bus, clock, membership, transitions, _, npc) = Build(alreadyChoking: true);
        MakeSys(transitions, bus, clock, em).Update(em, 1f);

        // ChokingComponent should NOT be added (idempotent guard hit IsChokingTag)
        Assert.False(npc.Has<ChokingComponent>());
    }

    // -- AT-04: Deceased NPC → no choke ---------------------------------------

    [Fact]
    public void AT04_DeceasedNpc_NoChokeTrigger()
    {
        var (em, bus, clock, membership, transitions, _, npc) = Build(npcState: LS.Deceased);
        MakeSys(transitions, bus, clock, em).Update(em, 1f);

        Assert.False(npc.Has<IsChokingTag>());
    }

    // -- AT-05: No distraction → no choke -------------------------------------

    [Fact]
    public void AT05_NoDistractionConditions_NoChoke()
    {
        // Energy well above threshold; stress and irritation below thresholds
        var (em, bus, clock, membership, transitions, _, npc) = Build(
            energy: 90f, acuteLevel: 10, irritationCurrent: 10);
        MakeSys(transitions, bus, clock, em).Update(em, 1f);

        Assert.False(npc.Has<IsChokingTag>());
    }

    // -- AT-06: Low energy alone is sufficient ---------------------------------

    [Fact]
    public void AT06_LowEnergy_AloneIsSufficientDistraction()
    {
        // Energy < 40; stress and irritation both below their thresholds
        var (em, bus, clock, membership, transitions, _, npc) = Build(
            energy: 20f, acuteLevel: 0, irritationCurrent: 0);
        MakeSys(transitions, bus, clock, em).Update(em, 1f);

        Assert.True(npc.Has<IsChokingTag>());
    }

    // -- AT-07: High stress alone is sufficient --------------------------------

    [Fact]
    public void AT07_HighStress_AloneIsSufficientDistraction()
    {
        // Energy fine; stress >= 70; irritation fine
        var (em, bus, clock, membership, transitions, _, npc) = Build(
            energy: 90f, acuteLevel: 75, irritationCurrent: 0);
        MakeSys(transitions, bus, clock, em).Update(em, 1f);

        Assert.True(npc.Has<IsChokingTag>());
    }

    // -- AT-08: High irritation alone is sufficient ----------------------------

    [Fact]
    public void AT08_HighIrritation_AloneIsSufficientDistraction()
    {
        // Energy fine; stress fine; irritation >= 65
        var (em, bus, clock, membership, transitions, _, npc) = Build(
            energy: 90f, acuteLevel: 0, irritationCurrent: 70);
        MakeSys(transitions, bus, clock, em).Update(em, 1f);

        Assert.True(npc.Has<IsChokingTag>());
    }

    // -- AT-09: ChokeStarted narrative emitted ---------------------------------

    [Fact]
    public void AT09_EmitNarrativeTrue_ChokeStartedCandidateEmitted()
    {
        var (em, bus, clock, membership, transitions, _, _) = Build();
        var cfg = DefaultCfg();
        cfg.EmitChokeStartedNarrative = true;

        var candidates = Collect(bus, () =>
            MakeSys(transitions, bus, clock, em, cfg).Update(em, 1f));

        Assert.Contains(candidates, c => c.Kind == NarrativeEventKind.ChokeStarted);
    }

    // -- AT-10: No narrative when flag is off ----------------------------------

    [Fact]
    public void AT10_EmitNarrativeFalse_NoChokeStartedCandidate()
    {
        var (em, bus, clock, membership, transitions, _, _) = Build();
        var cfg = DefaultCfg();
        cfg.EmitChokeStartedNarrative = false;

        var candidates = Collect(bus, () =>
            MakeSys(transitions, bus, clock, em, cfg).Update(em, 1f));

        Assert.DoesNotContain(candidates, c => c.Kind == NarrativeEventKind.ChokeStarted);
    }

    // -- AT-11: ChokingComponent.BolusSize mirrors Toughness ------------------

    [Fact]
    public void AT11_ChokingComponent_BolusSize_MirrorsBolusComponentToughness()
    {
        const float toughness = 0.80f;
        var (em, bus, clock, membership, transitions, _, npc) = Build(bolusThoughness: toughness);
        MakeSys(transitions, bus, clock, em).Update(em, 1f);

        Assert.True(npc.Has<ChokingComponent>());
        Assert.Equal(toughness, npc.Get<ChokingComponent>().BolusSize);
    }

    // -- AT-12: MoodComponent.PanicLevel set to config value ------------------

    [Fact]
    public void AT12_PanicLevel_SetToPanicMoodIntensity()
    {
        var (em, bus, clock, membership, transitions, _, npc) = Build();
        MakeSys(transitions, bus, clock, em).Update(em, 1f);

        Assert.Equal(DefaultCfg().PanicMoodIntensity, npc.Get<MoodComponent>().PanicLevel);
    }

    // -- AT-13: LifeStateTransitionSystem drains request in same tick ---------

    [Fact]
    public void AT13_LifeStateTransition_Drains_ChokingNpc_BecomesIncapacitated()
    {
        var (em, bus, clock, membership, transitions, _, npc) = Build();

        // Detection + drain in the same conceptual tick
        MakeSys(transitions, bus, clock, em).Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.Equal(LS.Incapacitated, npc.Get<LifeStateComponent>().State);
    }
}
