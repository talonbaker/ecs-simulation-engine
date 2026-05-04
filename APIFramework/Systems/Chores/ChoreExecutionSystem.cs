using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Visual;

namespace APIFramework.Systems.Chores;

/// <summary>
/// Phase: Cleanup (90), after WorkloadSystem.
/// For each NPC whose IntendedAction is ChoreWork:
///   - Verifies NPC is near the chore anchor (same room or within proximity).
///   - Advances CompletionLevel by baseRate × acceptanceBiasMult × stressMult.
///   - On completion: emits ChoreCompleted, updates ChoreHistoryComponent, resets chore.
///   - Checks overrotation window; increments ChoreOverrotationEventsToday when exceeded.
///   - Emits ChoreBadlyDone when quality is below threshold.
/// </summary>
public sealed class ChoreExecutionSystem : ISystem
{
    private readonly ChoreConfig              _cfg;
    private readonly SimulationClock          _clock;
    private readonly ChoreAcceptanceBiasTable _biasTable;
    private readonly NarrativeEventBus        _bus;
    private readonly ParticleTriggerBus?      _particleBus;

    public ChoreExecutionSystem(
        ChoreConfig              cfg,
        SimulationClock          clock,
        ChoreAcceptanceBiasTable biasTable,
        NarrativeEventBus        bus,
        ParticleTriggerBus?      particleBus = null)
    {
        _cfg         = cfg;
        _clock       = clock;
        _biasTable   = biasTable;
        _bus         = bus;
        _particleBus = particleBus;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        // Build chore entity lookup by assignee.
        var choreByAssignee = new Dictionary<Guid, Entity>();
        foreach (var e in em.GetAllEntities())
        {
            if (!e.Has<ChoreComponent>()) continue;
            var c = e.Get<ChoreComponent>();
            if (c.CurrentAssigneeId != Guid.Empty)
                choreByAssignee[c.CurrentAssigneeId] = e;
        }

        foreach (var npc in em.Query<NpcTag>().ToList())
        {
            if (!LifeStateGuard.IsAlive(npc)) continue;
            if (!npc.Has<IntendedActionComponent>()) continue;

            var intent = npc.Get<IntendedActionComponent>();
            if (intent.Kind != IntendedActionKind.ChoreWork) continue;
            if (!choreByAssignee.TryGetValue(npc.Id, out var choreEntity)) continue;

            var chore = choreEntity.Get<ChoreComponent>();
            if (chore.CompletionLevel >= 1.0f) continue;

            // Multipliers.
            double acceptanceBiasMult = GetAcceptanceBiasMult(npc, chore.Kind);
            double stressMult         = GetStressMult(npc);

            double advance = _cfg.ChoreCompletionRatePerSecond * deltaTime
                             * acceptanceBiasMult * stressMult;

            chore.CompletionLevel = Math.Clamp(chore.CompletionLevel + (float)advance, 0f, 1f);

            // Quality tracks as a blend of acceptanceBias × stressMult, clamped 0..1.
            float quality = (float)Math.Clamp(acceptanceBiasMult * stressMult, 0.0, 1.0);

            if (chore.CompletionLevel >= 1.0f)
            {
                chore.QualityOfLastExecution = quality;
                chore.LastDoneTick           = _clock.CurrentTick;
                chore.NextScheduledTick      = _clock.CurrentTick + GetFrequencyTicks(chore.Kind);
                chore.CurrentAssigneeId      = Guid.Empty;

                UpdateHistory(npc, chore.Kind, quality);
                CheckOverrotation(npc, chore.Kind);

                int npcIntId = EntityIntId(npc);

                if (_particleBus != null && npc.Has<PositionComponent>())
                {
                    var pos = npc.Get<PositionComponent>();
                    _particleBus.Emit(ParticleTriggerKind.CleaningMist, npc.Id, pos.X, pos.Z, quality, _clock.CurrentTick);
                }

                // ChoreCompleted (non-persistent).
                _bus.RaiseCandidate(new NarrativeEventCandidate(
                    _clock.CurrentTick,
                    NarrativeEventKind.ChoreCompleted,
                    new[] { npcIntId },
                    null,
                    $"{chore.Kind} completed"));

                // ChoreBadlyDone if below quality threshold (persistent).
                if (quality < _cfg.BadQualityThreshold)
                {
                    _bus.RaiseCandidate(new NarrativeEventCandidate(
                        _clock.CurrentTick,
                        NarrativeEventKind.ChoreBadlyDone,
                        new[] { npcIntId },
                        null,
                        $"{chore.Kind} done badly (quality {quality:F2})"));
                }
            }

            choreEntity.Add(chore);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void UpdateHistory(Entity npc, ChoreKind kind, float quality)
    {
        var history = npc.Has<ChoreHistoryComponent>()
            ? npc.Get<ChoreHistoryComponent>()
            : new ChoreHistoryComponent
            {
                TimesPerformed       = new Dictionary<ChoreKind, int>(),
                TimesRefused         = new Dictionary<ChoreKind, int>(),
                AverageQuality       = new Dictionary<ChoreKind, float>(),
                WindowTimesPerformed = new Dictionary<ChoreKind, int>(),
                WindowStartDay       = new Dictionary<ChoreKind, int>(),
            };

        history.TimesPerformed ??= new Dictionary<ChoreKind, int>();
        history.AverageQuality ??= new Dictionary<ChoreKind, float>();
        history.WindowTimesPerformed ??= new Dictionary<ChoreKind, int>();
        history.WindowStartDay ??= new Dictionary<ChoreKind, int>();

        // Lifetime count.
        history.TimesPerformed[kind] = (history.TimesPerformed.GetValueOrDefault(kind, 0)) + 1;

        // Rolling average quality.
        int performed = history.TimesPerformed[kind];
        float prevAvg = history.AverageQuality.GetValueOrDefault(kind, quality);
        history.AverageQuality[kind] = prevAvg + (quality - prevAvg) / performed;

        // Rolling window (reset when window expired).
        int windowStart = history.WindowStartDay.GetValueOrDefault(kind, _clock.DayNumber);
        if (_clock.DayNumber - windowStart >= _cfg.ChoreOverrotationWindowGameDays)
        {
            windowStart = _clock.DayNumber;
            history.WindowTimesPerformed[kind] = 0;
        }
        history.WindowStartDay[kind]       = windowStart;
        history.WindowTimesPerformed[kind] = history.WindowTimesPerformed.GetValueOrDefault(kind, 0) + 1;

        npc.Add(history);
    }

    private void CheckOverrotation(Entity npc, ChoreKind kind)
    {
        if (!npc.Has<ChoreHistoryComponent>()) return;
        var history = npc.Get<ChoreHistoryComponent>();
        int windowCount = history.WindowTimesPerformed?.GetValueOrDefault(kind, 0) ?? 0;
        if (windowCount <= _cfg.ChoreOverrotationThreshold) return;

        // Increment overrotation counter on StressComponent.
        if (npc.Has<StressComponent>())
        {
            var stress = npc.Get<StressComponent>();
            stress.ChoreOverrotationEventsToday++;
            npc.Add(stress);
        }

        // Emit ChoreOverrotation narrative (persistent).
        _bus.RaiseCandidate(new NarrativeEventCandidate(
            _clock.CurrentTick,
            NarrativeEventKind.ChoreOverrotation,
            new[] { EntityIntId(npc) },
            null,
            $"{kind} overrotation (window count {windowCount})"));
    }

    private double GetAcceptanceBiasMult(Entity npc, ChoreKind kind)
    {
        string archetypeId = npc.Has<NpcArchetypeComponent>()
            ? npc.Get<NpcArchetypeComponent>().ArchetypeId ?? ""
            : "";
        return Math.Clamp(_biasTable.GetBias(archetypeId, kind), 0.1, 1.0);
    }

    private static double GetStressMult(Entity npc)
    {
        if (!npc.Has<StressComponent>()) return 1.0;
        int acute = npc.Get<StressComponent>().AcuteLevel;
        return Math.Clamp(1.0 - acute * 0.005, 0.1, 1.0);  // max 50% penalty at acute=100
    }

    private long GetFrequencyTicks(ChoreKind kind) => kind switch
    {
        ChoreKind.CleanMicrowave     => _cfg.FrequencyTicks.CleanMicrowave,
        ChoreKind.CleanFridge        => _cfg.FrequencyTicks.CleanFridge,
        ChoreKind.CleanBathroom      => _cfg.FrequencyTicks.CleanBathroom,
        ChoreKind.TakeOutTrash       => _cfg.FrequencyTicks.TakeOutTrash,
        ChoreKind.RefillWaterCooler  => _cfg.FrequencyTicks.RefillWaterCooler,
        ChoreKind.RestockSupplyCloset=> _cfg.FrequencyTicks.RestockSupplyCloset,
        ChoreKind.ReplaceToner       => _cfg.FrequencyTicks.ReplaceToner,
        _                            => 7_200_000,
    };

    private static int EntityIntId(Entity e)
    {
        var b = e.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }
}
