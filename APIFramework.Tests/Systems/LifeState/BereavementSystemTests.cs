using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.LifeState;

/// <summary>
/// AT-04: Death event with a witness (second participant) → WitnessedDeathEventsToday incremented.
/// AT-05: Death event with a witness → GriefLevel spiked to at least WitnessGriefIntensity.
/// AT-06: Colleague with Intensity >= BereavementMinIntensity → BereavementEventsToday incremented.
/// AT-07: Colleague with Intensity < BereavementMinIntensity → no effect (below threshold guard).
/// AT-08: Colleague GriefLevel scaled proportionally to relationship intensity fraction.
/// AT-09: BereavementImpact narrative candidate emitted for each qualifying colleague.
/// AT-10: Witness is not double-counted as a colleague (already handled; no BereavementImpact for them).
/// AT-11: Deceased NPC (no LifeState guard check needed here — colleague path checks IsAlive).
/// </summary>
public class BereavementSystemTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BereavementConfig DefaultCfg() => new()
    {
        WitnessedDeathStressGain           = 20.0,
        BereavementStressGain              = 5.0,
        WitnessGriefIntensity             = 80f,
        ColleagueBereavementGriefIntensity = 40f,
        BereavementMinIntensity           = 20,
        ProximityBereavementMinIntensity   = 30,
        ProximityBereavementStressGain     = 8.0,
    };

    private static int EntityIntId(Entity e)
    {
        var b = e.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    private static (EntityManager em, NarrativeEventBus bus, SimulationClock clock) BaseWorld()
    {
        var em    = new EntityManager();
        var bus   = new NarrativeEventBus();
        var clock = new SimulationClock();
        return (em, bus, clock);
    }

    private static Entity MakeNpc(EntityManager em, LS state = LS.Alive)
    {
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = state });
        npc.Add(new StressComponent());
        npc.Add(new MoodComponent());
        return npc;
    }

    /// <summary>
    /// Creates a canonical relationship entity between two NPCs.
    /// Intensity is stored by RelationshipComponent(ctor) which canonicalises A &lt; B.
    /// </summary>
    private static Entity MakeRelationship(EntityManager em, Entity a, Entity b, int intensity = 50)
    {
        var rel = em.CreateEntity();
        rel.Add(new RelationshipTag());
        rel.Add(new RelationshipComponent(EntityIntId(a), EntityIntId(b), intensity: intensity));
        return rel;
    }

    private static List<NarrativeEventCandidate> Collect(NarrativeEventBus bus, Action raise)
    {
        var list = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += list.Add;
        raise();
        bus.OnCandidateEmitted -= list.Add;
        return list;
    }

    // ── AT-04: Witness → WitnessedDeathEventsToday ───────────────────────────

    [Fact]
    public void AT04_DeathWithWitness_IncrementsWitnessedDeathEventsToday()
    {
        var (em, bus, clock) = BaseWorld();
        var deceased = MakeNpc(em, LS.Deceased);
        var witness  = MakeNpc(em);

        _ = new BereavementSystem(bus, em, clock, DefaultCfg());

        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.Died,
            ParticipantIds: new[] { EntityIntId(deceased), EntityIntId(witness) },
            RoomId:         null,
            Detail:         "test death"));

        Assert.Equal(1, witness.Get<StressComponent>().WitnessedDeathEventsToday);
    }

    // ── AT-05: Witness → GriefLevel spikes ───────────────────────────────────

    [Fact]
    public void AT05_DeathWithWitness_GriefLevel_AtLeastWitnessGriefIntensity()
    {
        var (em, bus, clock) = BaseWorld();
        var deceased = MakeNpc(em, LS.Deceased);
        var witness  = MakeNpc(em);

        _ = new BereavementSystem(bus, em, clock, DefaultCfg());

        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.Choked,
            ParticipantIds: new[] { EntityIntId(deceased), EntityIntId(witness) },
            RoomId:         null,
            Detail:         "choke death"));

        Assert.True(witness.Get<MoodComponent>().GriefLevel >= DefaultCfg().WitnessGriefIntensity);
    }

    [Fact]
    public void AT05_GriefLevel_UsesMax_DoesNotLowerExistingHighGrief()
    {
        var (em, bus, clock) = BaseWorld();
        var deceased = MakeNpc(em, LS.Deceased);
        var witness  = MakeNpc(em);

        // Pre-existing grief higher than WitnessGriefIntensity (80)
        var mood = witness.Get<MoodComponent>();
        mood.GriefLevel = 95f;
        witness.Add(mood);

        _ = new BereavementSystem(bus, em, clock, DefaultCfg());

        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.Died,
            ParticipantIds: new[] { EntityIntId(deceased), EntityIntId(witness) },
            RoomId:         null,
            Detail:         "test"));

        Assert.Equal(95f, witness.Get<MoodComponent>().GriefLevel); // max preserves higher value
    }

    // ── AT-06: Colleague above threshold → BereavementEventsToday ────────────

    [Fact]
    public void AT06_Colleague_AboveThreshold_IncrementsBereavementEventsToday()
    {
        var (em, bus, clock) = BaseWorld();
        var deceased  = MakeNpc(em, LS.Deceased);
        var colleague = MakeNpc(em);
        MakeRelationship(em, deceased, colleague, intensity: 50); // 50 >= 20 (threshold)

        _ = new BereavementSystem(bus, em, clock, DefaultCfg());

        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.Died,
            ParticipantIds: new[] { EntityIntId(deceased) }, // no witness
            RoomId:         null,
            Detail:         "test"));

        Assert.Equal(1, colleague.Get<StressComponent>().BereavementEventsToday);
    }

    // ── AT-07: Colleague below threshold → skipped ───────────────────────────

    [Fact]
    public void AT07_Colleague_BelowThreshold_NoBereavementEffect()
    {
        var (em, bus, clock) = BaseWorld();
        var deceased  = MakeNpc(em, LS.Deceased);
        var colleague = MakeNpc(em);
        MakeRelationship(em, deceased, colleague, intensity: 10); // 10 < 20 (threshold)

        _ = new BereavementSystem(bus, em, clock, DefaultCfg());

        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.Died,
            ParticipantIds: new[] { EntityIntId(deceased) },
            RoomId:         null,
            Detail:         "test"));

        Assert.Equal(0, colleague.Get<StressComponent>().BereavementEventsToday);
        Assert.Equal(0f, colleague.Get<MoodComponent>().GriefLevel);
    }

    // ── AT-08: Colleague GriefLevel scaled by intensity fraction ─────────────

    [Fact]
    public void AT08_Colleague_GriefLevel_ScaledByIntensityFraction()
    {
        var (em, bus, clock) = BaseWorld();
        var deceased  = MakeNpc(em, LS.Deceased);
        var colleague = MakeNpc(em);
        MakeRelationship(em, deceased, colleague, intensity: 50); // fraction = 0.5

        var cfg = DefaultCfg(); // ColleagueBereavementGriefIntensity = 40f
        // Expected GriefLevel = 40f * 0.5f = 20f
        _ = new BereavementSystem(bus, em, clock, cfg);

        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.Died,
            ParticipantIds: new[] { EntityIntId(deceased) },
            RoomId:         null,
            Detail:         "test"));

        float expected = (float)(cfg.ColleagueBereavementGriefIntensity * (50 / 100f));
        Assert.Equal(expected, colleague.Get<MoodComponent>().GriefLevel, precision: 3);
    }

    // ── AT-09: BereavementImpact narrative emitted for colleague ──────────────

    [Fact]
    public void AT09_Colleague_BereavementImpact_NarrativeEmitted()
    {
        var (em, bus, clock) = BaseWorld();
        var deceased  = MakeNpc(em, LS.Deceased);
        var colleague = MakeNpc(em);
        MakeRelationship(em, deceased, colleague, intensity: 50);

        _ = new BereavementSystem(bus, em, clock, DefaultCfg());

        var allCandidates = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += allCandidates.Add;

        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.Died,
            ParticipantIds: new[] { EntityIntId(deceased) },
            RoomId:         null,
            Detail:         "test"));

        bus.OnCandidateEmitted -= allCandidates.Add;

        Assert.Contains(allCandidates, c => c.Kind == NarrativeEventKind.BereavementImpact);
    }

    // ── AT-10: Witness is not also counted as a colleague ────────────────────

    [Fact]
    public void AT10_Witness_NotDoubleCounted_NoBereavementImpactForWitness()
    {
        var (em, bus, clock) = BaseWorld();
        var deceased = MakeNpc(em, LS.Deceased);
        var witness  = MakeNpc(em);

        // Relationship exists between the deceased and witness, intensity above threshold
        MakeRelationship(em, deceased, witness, intensity: 60);

        _ = new BereavementSystem(bus, em, clock, DefaultCfg());

        var allCandidates = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += allCandidates.Add;

        // Death event — witness is second participant
        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.Died,
            ParticipantIds: new[] { EntityIntId(deceased), EntityIntId(witness) },
            RoomId:         null,
            Detail:         "test"));

        bus.OnCandidateEmitted -= allCandidates.Add;

        // BereavementImpact should NOT appear for the witness (they were handled by witness path)
        int witnessIntId = EntityIntId(witness);
        Assert.DoesNotContain(allCandidates, c =>
            c.Kind == NarrativeEventKind.BereavementImpact &&
            c.ParticipantIds.Count > 0 &&
            c.ParticipantIds[0] == witnessIntId);
    }
}
