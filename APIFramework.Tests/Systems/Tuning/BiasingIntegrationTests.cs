using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Tuning;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.Tuning;

/// <summary>
/// AT-04: Newbie chokes statistically more than Old Hand under identical conditions.
/// AT-05: Vent's bereavement grief accumulates faster than Cynic's under identical witness conditions.
/// AT-06: Old Hand's slip chance is statistically lower than Newbie's.
/// AT-07: Cynic's persistent memory entries decay faster than Old Hand's (smaller effective buffer).
/// </summary>
public class BiasingIntegrationTests
{
    private static TuningCatalog Catalog() => TuningCatalog.LoadFromDirectory();

    private static int EntityIntId(Entity e)
    {
        var b = e.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Entity MakeNpc(EntityManager em, string archetypeId, LS state = LS.Alive)
    {
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = archetypeId });
        npc.Add(new LifeStateComponent { State = state });
        npc.Add(new StressComponent());
        npc.Add(new MoodComponent());
        npc.Add(new WillpowerComponent { Current = 80, Baseline = 80 });
        npc.Add(new PositionComponent { X = 5f, Z = 5f });
        npc.Add(new MovementComponent { SpeedModifier = 1.0f });
        return npc;
    }

    // ── AT-04: Newbie chokes at lower bolus toughness than Old Hand ───────────

    [Fact]
    public void AT04_Newbie_ChokesAtLowerBolusToughness_ThanOldHand()
    {
        // BolusSizeThreshold default = 0.65.
        // Old Hand bolusSizeThresholdMult = 1.20 → effective = 0.78
        // Newbie   bolusSizeThresholdMult = 0.85 → effective = 0.5525
        // Bolus toughness 0.70: Newbie chokes (0.70 >= 0.5525), Old Hand does not (0.70 < 0.78).
        const float bolusSize = 0.70f;
        var catalog = Catalog();

        var em    = new EntityManager();
        var bus   = new NarrativeEventBus();
        var clock = new SimulationClock();
        var cfg   = new SimConfig { LifeState = new LifeStateConfig { DefaultIncapacitatedTicks = 180 } };
        var trans = new LifeStateTransitionSystem(bus, em, clock, cfg);

        var chokeCfg = new ChokingConfig
        {
            BolusSizeThreshold = 0.65f,
            EnergyThreshold    = 40,
            StressThreshold    = 70,
            IrritationThreshold = 65,
            IncapacitationTicks = 90,
            PanicMoodIntensity = 0.85f,
            EmitChokeStartedNarrative = false,
        };

        // Helper: build a bolus-NPC pair with the archetype, run system, return whether choke fired.
        bool DidChoke(string archetypeId)
        {
            var npc = MakeNpc(em, archetypeId);
            // Low energy = distraction trigger.
            npc.Add(new EnergyComponent { Energy = 20f });

            var bolus = em.CreateEntity();
            bolus.Add(new EsophagusTransitComponent { TargetEntityId = npc.Id });
            bolus.Add(new BolusComponent { Toughness = bolusSize });

            var sys = new ChokingDetectionSystem(trans, bus, clock, chokeCfg, em, tuning: catalog);
            sys.Update(em, 0f);

            return npc.Has<IsChokingTag>();
        }

        bool newbieChoked  = DidChoke("the-newbie");
        bool oldHandChoked = DidChoke("the-old-hand");

        Assert.True(newbieChoked,   "Newbie should choke with bolus toughness 0.70 (effective threshold ~0.55)");
        Assert.False(oldHandChoked, "Old Hand should NOT choke with bolus toughness 0.70 (effective threshold ~0.78)");
    }

    // ── AT-05: Vent grieves harder than Cynic ─────────────────────────────────

    [Fact]
    public void AT05_Vent_GriefLevel_HigherThan_Cynic_WhenWitnessingDeath()
    {
        var catalog = Catalog();
        var em    = new EntityManager();
        var bus   = new NarrativeEventBus();
        var clock = new SimulationClock();

        var cfg = new BereavementConfig
        {
            WitnessedDeathStressGain           = 10.0,
            BereavementStressGain              = 5.0,
            WitnessGriefIntensity             = 60.0,
            ColleagueBereavementGriefIntensity = 30.0,
            BereavementMinIntensity           = 10,
        };

        _ = new BereavementSystem(bus, em, clock, cfg, catalog);

        var deceased = MakeNpc(em, "the-newbie", LS.Deceased);
        var vent     = MakeNpc(em, "the-vent");
        var cynic    = MakeNpc(em, "the-cynic");

        // Fire death event with Vent as witness.
        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.Died,
            ParticipantIds: new[] { EntityIntId(deceased), EntityIntId(vent) },
            RoomId:         null,
            Detail:         "test death with vent witness"));

        float ventGrief = vent.Get<MoodComponent>().GriefLevel;

        // Reset vent grief, fire again with Cynic as witness.
        var ventMood = vent.Get<MoodComponent>();
        ventMood.GriefLevel = 0f;
        vent.Add(ventMood);

        var deceased2 = MakeNpc(em, "the-newbie", LS.Deceased);
        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           2,
            Kind:           NarrativeEventKind.Died,
            ParticipantIds: new[] { EntityIntId(deceased2), EntityIntId(cynic) },
            RoomId:         null,
            Detail:         "test death with cynic witness"));

        float cynicGrief = cynic.Get<MoodComponent>().GriefLevel;

        Assert.True(ventGrief > cynicGrief,
            $"Vent should grieve harder than Cynic (vent={ventGrief}, cynic={cynicGrief})");
        Assert.True(cynicGrief > 0f, "Cynic should still have some grief (just less)");
    }

    // ── AT-06: Old Hand slips less than Newbie ────────────────────────────────

    [Fact]
    public void AT06_OldHand_EffectiveSlipChance_LowerThan_Newbie()
    {
        // Verify via catalog: the multipliers directly establish that Old Hand slips less.
        // Over a large number of (npcId, hazard, tick) samples, Old Hand's slipChanceMult=0.70
        // guarantees statistically fewer slips than Newbie's slipChanceMult=1.30.
        var catalog = Catalog();

        var oldHandBias = catalog.GetSlipBias("the-old-hand");
        var newbieBias  = catalog.GetSlipBias("the-newbie");

        // The multiplier gap is 0.70 vs 1.30 — confirm it's large enough to be statistically decisive.
        Assert.True(oldHandBias.SlipChanceMult < newbieBias.SlipChanceMult,
            "Old Hand slipChanceMult should be < Newbie slipChanceMult");

        // Run against a live system: spawn 200 Newbie NPCs and 200 Old Hand NPCs on the same
        // hazard tile for 1 tick each (unique entity IDs → different hash seeds → varied rolls).
        // RiskLevel=0.50 → Newbie slipChance≈0.715, OldHand≈0.315; gap is >8σ over 200 samples.
        var em     = new EntityManager();
        var bus    = new NarrativeEventBus();
        var clock  = new SimulationClock { TimeScale = 1f };
        var cfg    = new SimConfig { SlipAndFall = new SlipAndFallConfig { GlobalSlipChanceScale = 1.0f, StressDangerThreshold = 60, StressSlipMultiplier = 2.0f } };
        var trans  = new LifeStateTransitionSystem(bus, em, clock, cfg);
        var rng    = new SeededRandom(42);

        var hazard = em.CreateEntity();
        hazard.Add(new FallRiskComponent { RiskLevel = 0.50f });
        hazard.Add(new PositionComponent { X = 5f, Z = 5f });

        int newbieDeaths = 0, oldHandDeaths = 0;
        const int nSamples = 200;

        for (int i = 0; i < nSamples; i++)
        {
            var newbie = MakeNpc(em, "the-newbie");
            var slip1 = new SlipAndFallSystem(em, clock, cfg, trans, rng, tuning: catalog);
            slip1.Update(em, 1f);
            trans.Update(em, 1f);   // drain queue so transitions are applied
            if (!LifeStateGuard.IsAlive(newbie)) newbieDeaths++;
            else { em.DestroyEntity(newbie); }

            var oldHand = MakeNpc(em, "the-old-hand");
            var slip2 = new SlipAndFallSystem(em, clock, cfg, trans, rng, tuning: catalog);
            slip2.Update(em, 1f);
            trans.Update(em, 1f);   // drain queue so transitions are applied
            if (!LifeStateGuard.IsAlive(oldHand)) oldHandDeaths++;
            else { em.DestroyEntity(oldHand); }

            clock.Tick(1f / 20f);
        }

        Assert.True(oldHandDeaths < newbieDeaths,
            $"Old Hand should slip less than Newbie over {nSamples} samples " +
            $"(oldHand={oldHandDeaths}, newbie={newbieDeaths}; expected ~63 vs ~143)");
    }

    // ── AT-07: Cynic's memory decays faster than Old Hand's ──────────────────

    [Fact]
    public void AT07_Cynic_PersonalMemory_SmallerBuffer_ThanOldHand()
    {
        // The Cynic has decayRateMult=1.20, Old Hand has 0.85.
        // With MaxPersonalMemoryCount=10:
        //   Cynic effective cap = floor(10 / 1.20) = 8
        //   Old Hand effective cap = floor(10 / 0.85) = 11 (but capped at MaxPersonalMemoryCount=10 by system)
        // After 10 events: Cynic retains 8, Old Hand retains 10.
        var catalog = Catalog();
        var em     = new EntityManager();
        var bus    = new NarrativeEventBus();
        var cfg    = new MemoryConfig { MaxPersonalMemoryCount = 10, MaxRelationshipMemoryCount = 20 };

        _ = new MemoryRecordingSystem(bus, em, cfg, catalog);

        var cynic   = em.CreateEntity();
        cynic.Add(new NpcTag());
        cynic.Add(new NpcArchetypeComponent { ArchetypeId = "the-cynic" });

        var oldHand = em.CreateEntity();
        oldHand.Add(new NpcTag());
        oldHand.Add(new NpcArchetypeComponent { ArchetypeId = "the-old-hand" });

        int cynicId   = EntityIntId(cynic);
        int oldHandId = EntityIntId(oldHand);

        // Emit 12 events for each NPC.
        for (int i = 0; i < 12; i++)
        {
            bus.RaiseCandidate(new NarrativeEventCandidate(
                Tick:           i + 1,
                Kind:           NarrativeEventKind.OverdueTask,   // persistent kind
                ParticipantIds: new[] { cynicId },
                RoomId:         null,
                Detail:         $"cynic event {i}"));

            bus.RaiseCandidate(new NarrativeEventCandidate(
                Tick:           i + 1,
                Kind:           NarrativeEventKind.OverdueTask,
                ParticipantIds: new[] { oldHandId },
                RoomId:         null,
                Detail:         $"old-hand event {i}"));
        }

        int cynicCount   = cynic.Has<PersonalMemoryComponent>()
            ? cynic.Get<PersonalMemoryComponent>().Recent.Count : 0;
        int oldHandCount = oldHand.Has<PersonalMemoryComponent>()
            ? oldHand.Get<PersonalMemoryComponent>().Recent.Count : 0;

        Assert.True(cynicCount < oldHandCount,
            $"Cynic should retain fewer memory entries than Old Hand due to higher decay rate " +
            $"(cynic={cynicCount}, oldHand={oldHandCount})");
    }
}
