using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;

namespace APIFramework.Systems;

/// <summary>
/// Accumulates cortisol-like stress from suppression events, drive spikes, and social conflict.
/// Manages the AcuteLevel → ChronicLevel rolling average, stress tags, and the loop-closing
/// amplification event that feeds back into WillpowerSystem.
///
/// Phase: Cleanup (80) — runs after WillpowerSystem (Cognition=30) has drained the queue
/// and after NarrativeEventDetector (Narrative=70) has emitted social signals.
///
/// Loop closure: when AcuteLevel ≥ stressedTagThreshold, one extra SuppressionTick lands in
/// WillpowerEventQueue each tick. WillpowerSystem picks it up next tick (Cognition=30).
/// </summary>
public class StressSystem : ISystem
{
    private readonly StressConfig        _cfg;
    private readonly WorkloadConfig      _workloadCfg;
    private readonly BereavementConfig   _bereavementCfg;
    private readonly SimulationClock     _clock;
    private readonly WillpowerEventQueue _queue;

    // Per-entity fractional accumulator for AcuteLevel decay (carries sub-integer remainder).
    private readonly Dictionary<Guid, double> _decayAccum = new();

    // Conflict participant IDs gathered from the narrative bus since the last Update.
    private HashSet<int> _pendingConflictIds = new();

    private readonly EntityManager _em;

    public StressSystem(StressConfig cfg, WorkloadConfig workloadCfg, SimulationClock clock,
        WillpowerEventQueue queue, NarrativeEventBus narrativeBus,
        EntityManager em, BereavementConfig? bereavementCfg = null)
    {
        _cfg            = cfg;
        _workloadCfg    = workloadCfg;
        _bereavementCfg = bereavementCfg ?? new BereavementConfig();
        _clock          = clock;
        _queue          = queue;
        _em             = em;
        narrativeBus.OnCandidateEmitted += OnNarrativeCandidate;
    }

    private void OnNarrativeCandidate(NarrativeEventCandidate c)
    {
        if (c.Kind == NarrativeEventKind.LeftRoomAbruptly)
        {
            foreach (var id in c.ParticipantIds)
                _pendingConflictIds.Add(id);
        }
    }

    public void Update(EntityManager em, float deltaTime)
    {
        // Snapshot and clear the pending conflict set for this tick.
        var conflictIds = _pendingConflictIds;
        _pendingConflictIds = new HashSet<int>();

        var drainedBatch = _queue.LastDrainedBatch;

        foreach (var entity in em.Query<NpcTag>().ToList())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
            if (!entity.Has<StressComponent>()) continue;

            var stress   = entity.Get<StressComponent>();
            int entityId = WillpowerSystem.EntityIntId(entity);

            double neuroFactor = 1.0;
            if (entity.Has<PersonalityComponent>())
                neuroFactor = 1.0 + entity.Get<PersonalityComponent>().Neuroticism * _cfg.NeuroticismStressFactor;

            // 1. Count suppression events WillpowerSystem processed this tick.
            int suppressionCount = 0;
            foreach (var sig in drainedBatch)
            {
                if (sig.EntityId == entityId && sig.Kind == WillpowerEventKind.SuppressionTick)
                    suppressionCount++;
            }
            if (suppressionCount > 0)
            {
                double gain = suppressionCount * _cfg.SuppressionStressGain * neuroFactor;
                stress.AcuteLevel = Math.Clamp(
                    (int)(stress.AcuteLevel + gain), 0, 100);
                stress.SuppressionEventsToday += suppressionCount;
            }

            // 2. Drive spikes (any drive whose Current exceeds Baseline by more than delta).
            if (entity.Has<SocialDrivesComponent>())
            {
                var drives     = entity.Get<SocialDrivesComponent>();
                int spikeCount = CountDriveSpikes(drives);
                if (spikeCount > 0)
                {
                    double gain = spikeCount * _cfg.DriveSpikeStressGain * neuroFactor;
                    stress.AcuteLevel = Math.Clamp(
                        (int)(stress.AcuteLevel + gain), 0, 100);
                    stress.DriveSpikeEventsToday += spikeCount;
                }
            }

            // 3. Social conflict signal from NarrativeBus (LeftRoomAbruptly candidates).
            if (conflictIds.Contains(entityId))
            {
                double gain = _cfg.SocialConflictStressGain * neuroFactor;
                stress.AcuteLevel = Math.Clamp(
                    (int)(stress.AcuteLevel + gain), 0, 100);
                stress.SocialConflictEventsToday++;
            }

            // 4. Overdue task stress gain — each overdue task contributes per tick.
            if (entity.Has<WorkloadComponent>())
            {
                var wl = entity.Get<WorkloadComponent>();
                var activeTasks = wl.ActiveTasks;
                if (activeTasks != null && activeTasks.Count > 0)
                {
                    int overdueCount = 0;
                    foreach (var taskGuid in activeTasks)
                    {
                        var taskEntity = FindEntityByGuid(taskGuid);
                        if (taskEntity != null && taskEntity.Has<OverdueTag>())
                            overdueCount++;
                    }
                    if (overdueCount > 0)
                    {
                        double gain = overdueCount * _workloadCfg.OverdueTaskStressGain * neuroFactor;
                        stress.AcuteLevel = Math.Clamp(
                            (int)(stress.AcuteLevel + gain), 0, 100);
                        stress.OverdueTaskEventsToday += overdueCount;
                    }
                }
            }

            // 4.1. Witnessed death — one-shot bereavement stress for witnesses (WP-3.0.2).
            // BereavementSystem sets WitnessedDeathEventsToday at death-event time.
            // Applied and cleared here so the gain fires exactly once, not every tick.
            if (stress.WitnessedDeathEventsToday > 0)
            {
                double gain = stress.WitnessedDeathEventsToday * _bereavementCfg.WitnessedDeathStressGain * neuroFactor;
                stress.AcuteLevel = Math.Clamp(
                    (int)(stress.AcuteLevel + gain), 0, 100);
                stress.WitnessedDeathEventsToday = 0; // one-shot: clear after application
            }

            // 4.2. Colleague bereavement — one-shot hit for non-witness colleagues (WP-3.0.2).
            if (stress.BereavementEventsToday > 0)
            {
                double gain = stress.BereavementEventsToday * _bereavementCfg.BereavementStressGain * neuroFactor;
                stress.AcuteLevel = Math.Clamp(
                    (int)(stress.AcuteLevel + gain), 0, 100);
                stress.BereavementEventsToday = 0; // one-shot: clear after application
            }

            // 5. Per-tick acute decay via fractional accumulator.
            if (!_decayAccum.TryGetValue(entity.Id, out var decayRemainder))
                decayRemainder = 0.0;
            decayRemainder += _cfg.AcuteDecayPerTick;
            int decayInt = (int)decayRemainder;
            decayRemainder -= decayInt;
            _decayAccum[entity.Id] = decayRemainder;
            stress.AcuteLevel = Math.Clamp(stress.AcuteLevel - decayInt, 0, 100);

            // 6. Per-day chronic update (fires once when DayNumber advances).
            if (stress.LastDayUpdated == 0)
            {
                // First time this NPC is processed — bootstrap the day counter.
                stress.LastDayUpdated = _clock.DayNumber;
            }
            else if (_clock.DayNumber > stress.LastDayUpdated)
            {
                stress.ChronicLevel = Math.Clamp(
                    (stress.ChronicLevel * 6 + stress.AcuteLevel) / 7.0, 0.0, 100.0);
                stress.SuppressionEventsToday    = 0;
                stress.DriveSpikeEventsToday     = 0;
                stress.SocialConflictEventsToday = 0;
                stress.OverdueTaskEventsToday    = 0;
                stress.WitnessedDeathEventsToday = 0; // WP-3.0.2 (already cleared on use)
                stress.BereavementEventsToday    = 0; // WP-3.0.2 (already cleared on use)
                stress.LastDayUpdated = _clock.DayNumber;
            }

            // 7. Tag updates.
            UpdateTags(entity, ref stress);

            // 8. Push amplification event if stressed — picked up by WillpowerSystem next tick.
            if (stress.AcuteLevel >= _cfg.StressedTagThreshold)
            {
                double range = 100 - _cfg.StressedTagThreshold;
                double scale = range > 0
                    ? (stress.AcuteLevel - _cfg.StressedTagThreshold) / range
                    : 1.0;
                double mag    = _cfg.StressAmplificationMagnitude * scale;
                int    magInt = Math.Max(1, (int)Math.Round(mag));
                _queue.Enqueue(new WillpowerEventSignal(
                    entityId, WillpowerEventKind.SuppressionTick, magInt));
            }

            entity.Add(stress);
        }
    }

    private Entity? FindEntityByGuid(Guid guid)
    {
        foreach (var e in _em.GetAllEntities())
            if (e.Id == guid) return e;
        return null;
    }

    private int CountDriveSpikes(SocialDrivesComponent drives)
    {
        int count = 0;
        int delta = _cfg.DriveSpikeStressDelta;
        if (drives.Belonging.Current  - drives.Belonging.Baseline  > delta) count++;
        if (drives.Status.Current     - drives.Status.Baseline     > delta) count++;
        if (drives.Affection.Current  - drives.Affection.Baseline  > delta) count++;
        if (drives.Irritation.Current - drives.Irritation.Baseline > delta) count++;
        if (drives.Attraction.Current - drives.Attraction.Baseline > delta) count++;
        if (drives.Trust.Current      - drives.Trust.Baseline      > delta) count++;
        if (drives.Suspicion.Current  - drives.Suspicion.Baseline  > delta) count++;
        if (drives.Loneliness.Current - drives.Loneliness.Baseline > delta) count++;
        return count;
    }

    private void UpdateTags(Entity entity, ref StressComponent stress)
    {
        // StressedTag
        if (stress.AcuteLevel >= _cfg.StressedTagThreshold)
            entity.Add(new StressedTag());
        else
            entity.Remove<StressedTag>();

        // OverwhelmedTag
        if (stress.AcuteLevel >= _cfg.OverwhelmedTagThreshold)
            entity.Add(new OverwhelmedTag());
        else
            entity.Remove<OverwhelmedTag>();

        // BurningOutTag — sticky for burningOutCooldownDays after ChronicLevel drops below threshold
        bool aboveThreshold = stress.ChronicLevel >= _cfg.BurningOutTagThreshold;
        bool withinCooldown = stress.BurnoutLastAppliedDay > 0
            && (_clock.DayNumber - stress.BurnoutLastAppliedDay) <= _cfg.BurningOutCooldownDays;

        if (aboveThreshold)
        {
            entity.Add(new BurningOutTag());
            stress.BurnoutLastAppliedDay = _clock.DayNumber;
        }
        else if (withinCooldown)
        {
            entity.Add(new BurningOutTag());
        }
        else
        {
            entity.Remove<BurningOutTag>();
        }
    }
}
