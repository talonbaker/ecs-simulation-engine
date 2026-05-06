using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;

namespace APIFramework.Systems.Chores;

/// <summary>
/// Phase: PreUpdate (0). Runs once per game-day at <see cref="ChoreConfig.ChoreCheckHourOfDay"/>.
/// For each ChoreComponent whose NextScheduledTick &lt;= CurrentTick and is unassigned,
/// scores alive NPC candidates by acceptance-bias + quality-bonus - overrotation-penalty,
/// assigns the highest scorer. Emits ChoreAssigned (non-persistent).
/// </summary>
public sealed class ChoreAssignmentSystem : ISystem
{
    private readonly ChoreConfig             _cfg;
    private readonly SimulationClock         _clock;
    private readonly ChoreAcceptanceBiasTable _biasTable;
    private readonly NarrativeEventBus       _bus;
    private int _lastCheckDay;

    public ChoreAssignmentSystem(
        ChoreConfig             cfg,
        SimulationClock         clock,
        ChoreAcceptanceBiasTable biasTable,
        NarrativeEventBus       bus)
    {
        _cfg       = cfg;
        _clock     = clock;
        _biasTable = biasTable;
        _bus       = bus;
        _lastCheckDay = -1;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        // Run once per game-day when clock crosses choreCheckHour.
        if (_clock.DayNumber == _lastCheckDay) return;
        if (_clock.GameHour < _cfg.ChoreCheckHourOfDay) return;
        _lastCheckDay = _clock.DayNumber;

        var chores = em.GetAllEntities()
            .Where(e => e.Has<ChoreComponent>())
            .ToList();

        var aliveNpcs = em.Query<NpcTag>()
            .Where(e => LifeStateGuard.IsAlive(e))
            .OrderBy(e => EntityIntId(e))  // deterministic
            .ToList();

        foreach (var choreEntity in chores)
        {
            var chore = choreEntity.Get<ChoreComponent>();
            if (_clock.CurrentTick < chore.NextScheduledTick) continue;
            if (chore.CurrentAssigneeId != Guid.Empty) continue;  // already assigned

            // Build scored candidate set.
            var best      = Guid.Empty;
            var bestScore = double.MinValue;

            foreach (var npc in aliveNpcs)
            {
                string archetypeId = npc.Has<NpcArchetypeComponent>()
                    ? npc.Get<NpcArchetypeComponent>().ArchetypeId ?? ""
                    : "";

                float bias = _biasTable.GetBias(archetypeId, chore.Kind);
                if (bias < _cfg.MinChoreAcceptanceBias) continue;

                double overrotationPenalty = GetOverrotationPenalty(npc, chore.Kind);
                double qualityBonus        = GetQualityBonus(npc, chore.Kind);

                double score = bias - overrotationPenalty + qualityBonus;
                if (score > bestScore)
                {
                    bestScore = score;
                    best      = npc.Id;
                }
            }

            if (best == Guid.Empty) continue;  // no eligible candidate; stays unassigned

            chore.CurrentAssigneeId = best;
            choreEntity.Add(chore);

            // Emit non-persistent ChoreAssigned.
            var assigneeIntId = EntityIntIdFromGuid(best, aliveNpcs);
            _bus.RaiseCandidate(new NarrativeEventCandidate(
                _clock.CurrentTick,
                NarrativeEventKind.ChoreAssigned,
                new[] { assigneeIntId },
                null,
                $"Chore {chore.Kind} assigned"));
        }
    }

    // ── Scoring helpers ───────────────────────────────────────────────────────

    private static double GetOverrotationPenalty(Entity npc, ChoreKind kind)
    {
        if (!npc.Has<ChoreHistoryComponent>()) return 0.0;
        var history = npc.Get<ChoreHistoryComponent>();
        int window = history.WindowTimesPerformed?.GetValueOrDefault(kind, 0) ?? 0;
        return window * 0.10;  // 0.10 penalty per recent completion
    }

    private static double GetQualityBonus(Entity npc, ChoreKind kind)
    {
        if (!npc.Has<ChoreHistoryComponent>()) return 0.0;
        var history = npc.Get<ChoreHistoryComponent>();
        float avg = history.AverageQuality?.GetValueOrDefault(kind, 0.5f) ?? 0.5f;
        return (avg - 0.5) * 0.20;  // ±0.10 bonus/penalty from quality track record
    }

    private static int EntityIntId(Entity e)
    {
        var b = e.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    private static int EntityIntIdFromGuid(Guid id, IEnumerable<Entity> npcs)
    {
        foreach (var e in npcs)
            if (e.Id == id) return EntityIntId(e);
        return 0;
    }
}
