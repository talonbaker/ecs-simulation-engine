using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Narrative;


namespace APIFramework.Systems.LifeState;

/// <summary>
/// NarrativeEventBus subscriber that applies the immediate bereavement impact to
/// witnesses and colleague NPCs when a death event propagates.
///
/// TWO PATHS
/// ─────────
/// 1. Witness path (ev.ParticipantIds.Count >= 2):
///    The second participant is the witness. They receive:
///    - StressComponent.WitnessedDeathEventsToday += 1
///      (StressSystem applies WitnessedDeathStressGain and clears the counter.)
///    - MoodComponent.GriefLevel = Max(current, BereavementConfig.WitnessGriefIntensity)
///    Note: the witness's persistent memory of the death comes from the original death event
///    (Choked / SlippedAndFell / StarvedAlone / Died), which MemoryRecordingSystem already
///    marks as persistent. No separate BereavementImpact event is emitted for the witness.
///
/// 2. Colleague path (all Alive NPCs with a relationship to the deceased):
///    For each NPC with RelationshipComponent.Intensity >= BereavementMinIntensity:
///    - StressComponent.BereavementEventsToday += 1
///    - MoodComponent.GriefLevel = Max(current, BereavementMoodIntensity × intensityFraction)
///    - A BereavementImpact narrative candidate is emitted: participants = [colleague, deceased].
///      MemoryRecordingSystem routes this to per-pair + personal memory with Persistent = true.
///
/// Deterministic: colleagues are iterated in ascending EntityIntId order.
/// No System.Random. Fires exactly once per death event.
///
/// Phase: Narrative (70) subscriber — fires synchronously from the narrative bus.
/// No per-tick Update logic; purely event-driven.
///
/// WP-3.0.2: Deceased-Entity Handling + Bereavement.
/// </summary>
/// <seealso cref="BereavementByProximitySystem"/>
/// <seealso cref="CorpseSpawnerSystem"/>
public sealed class BereavementSystem : ISystem
{
    private readonly EntityManager    _em;
    private readonly NarrativeEventBus _narrativeBus;
    private readonly SimulationClock  _clock;
    private readonly BereavementConfig _cfg;

    /// <summary>
    /// Subscribes to <see cref="NarrativeEventBus.OnCandidateEmitted"/> to react to death events.
    /// </summary>
    /// <param name="narrativeBus">Bus from which death events are received and on which BereavementImpact is published.</param>
    /// <param name="em">Entity manager — used to resolve participants by EntityIntId.</param>
    /// <param name="clock">Simulation clock — supplies <c>CurrentTick</c> for emitted candidates.</param>
    /// <param name="cfg">Bereavement config — supplies witness/colleague intensity values.</param>
    public BereavementSystem(
        NarrativeEventBus  narrativeBus,
        EntityManager      em,
        SimulationClock    clock,
        BereavementConfig  cfg)
    {
        _em           = em;
        _narrativeBus = narrativeBus;
        _clock        = clock;
        _cfg          = cfg;
        narrativeBus.OnCandidateEmitted += OnDeathEvent;
    }

    private void OnDeathEvent(NarrativeEventCandidate ev)
    {
        // Only react to the four death narrative kinds.
        if (ev.Kind is not (
            NarrativeEventKind.Choked         or
            NarrativeEventKind.SlippedAndFell or
            NarrativeEventKind.StarvedAlone   or
            NarrativeEventKind.Died))
            return;

        if (ev.ParticipantIds.Count == 0) return;

        int deceasedIntId = ev.ParticipantIds[0];
        var deceased = FindEntityByIntId(deceasedIntId);
        if (deceased is null) return;

        // ── 1. Witness path ───────────────────────────────────────────────────

        if (ev.ParticipantIds.Count >= 2)
        {
            var witness = FindEntityByIntId(ev.ParticipantIds[1]);
            if (witness != null && LifeStateGuard.IsAlive(witness))
            {
                // Stress counter — StressSystem applies and clears.
                if (witness.Has<StressComponent>())
                {
                    var stress = witness.Get<StressComponent>();
                    stress.WitnessedDeathEventsToday++;
                    witness.Add(stress);
                }

                // Grief spike — MoodSystem decays it via NegativeDecayRate.
                if (witness.Has<MoodComponent>())
                {
                    var mood = witness.Get<MoodComponent>();
                    mood.GriefLevel = MathF.Max(mood.GriefLevel, (float)_cfg.WitnessGriefIntensity);
                    witness.Add(mood);
                }
            }
        }

        // ── 2. Colleague path ─────────────────────────────────────────────────

        // Iterate relationship entities to find colleagues of the deceased.
        // Sort by colleague EntityIntId for deterministic ordering.
        var colleagues = _em.Query<RelationshipTag>()
            .Where(rel => rel.Has<RelationshipComponent>())
            .Select(rel => rel.Get<RelationshipComponent>())
            .Where(rc => rc.ParticipantA == deceasedIntId || rc.ParticipantB == deceasedIntId)
            .OrderBy(rc => rc.ParticipantA == deceasedIntId ? rc.ParticipantB : rc.ParticipantA)
            .ToList();

        foreach (var rc in colleagues)
        {
            // Skip below-threshold relationships — "they barely knew each other."
            if (rc.Intensity < _cfg.BereavementMinIntensity) continue;

            int colleagueIntId = rc.ParticipantA == deceasedIntId ? rc.ParticipantB : rc.ParticipantA;
            var colleague = FindEntityByIntId(colleagueIntId);
            if (colleague is null) continue;
            if (!LifeStateGuard.IsAlive(colleague)) continue;

            // Skip if this NPC was the witness (already handled above).
            if (ev.ParticipantIds.Count >= 2 && colleagueIntId == ev.ParticipantIds[1]) continue;

            // Intensity fraction: 0..1 (relationship intensity scaled from 0–100).
            float intensityFraction = rc.Intensity / 100f;

            // Stress counter.
            if (colleague.Has<StressComponent>())
            {
                var stress = colleague.Get<StressComponent>();
                stress.BereavementEventsToday++;
                colleague.Add(stress);
            }

            // Grief spike scaled by relationship intensity.
            if (colleague.Has<MoodComponent>())
            {
                var mood = colleague.Get<MoodComponent>();
                float griefAmount = (float)(_cfg.ColleagueBereavementGriefIntensity * intensityFraction);
                mood.GriefLevel = MathF.Max(mood.GriefLevel, griefAmount);
                colleague.Add(mood);
            }

            // Emit a BereavementImpact narrative candidate so MemoryRecordingSystem
            // routes this to per-pair + personal memory with Persistent = true.
            _narrativeBus.RaiseCandidate(new NarrativeEventCandidate(
                Tick:           _clock.CurrentTick,
                Kind:           NarrativeEventKind.BereavementImpact,
                ParticipantIds: new[] { colleagueIntId, deceasedIntId },
                RoomId:         GetRoomIdFromCauseOfDeath(deceased),
                Detail:         $"NPC {colleague.Id} is grieving the death of NPC {deceased.Id} (relationship intensity {rc.Intensity})."
            ));
        }
    }

    /// <summary>
    /// No per-tick work — this system is purely event-driven via the narrative bus subscription.
    /// </summary>
    /// <param name="em">Entity manager (unused).</param>
    /// <param name="deltaTime">Tick delta in seconds (unused).</param>
    public void Update(EntityManager em, float deltaTime) { /* event-driven; no per-tick work */ }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static string? GetRoomIdFromCauseOfDeath(Entity entity)
    {
        if (!entity.Has<CauseOfDeathComponent>()) return null;
        var roomId = entity.Get<CauseOfDeathComponent>().LocationRoomId;
        return roomId == Guid.Empty ? null : roomId.ToString();
    }

    private Entity? FindEntityByIntId(int intId)
    {
        foreach (var e in _em.GetAllEntities())
        {
            if (EntityIntId(e) == intId) return e;
        }
        return null;
    }

    private static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }
}
