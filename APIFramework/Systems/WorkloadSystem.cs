using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;

namespace APIFramework.Systems;

/// <summary>
/// Phase: Cleanup (80) — runs after ActionSelectionSystem (Cognition=30) has decided intents.
/// Per tick, for each NPC with WorkloadComponent:
///   1. Recomputes CurrentLoad.
///   2. Advances progress on the task the NPC intends to Work on.
///   3. Detects task completion (Progress ≥ 1.0) and emits TaskCompleted.
///   4. Detects newly overdue tasks and emits OverdueTask (once per transition).
/// </summary>
public class WorkloadSystem : ISystem
{
    private readonly WorkloadConfig    _cfg;
    private readonly SimulationClock   _clock;
    private readonly NarrativeEventBus _bus;
    private readonly EntityManager     _em;

    public WorkloadSystem(WorkloadConfig cfg, SimulationClock clock,
        NarrativeEventBus bus, EntityManager em)
    {
        _cfg   = cfg;
        _clock = clock;
        _bus   = bus;
        _em    = em;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        long now = (long)_clock.TotalTime;

        foreach (var npc in em.Query<NpcTag>().ToList())
        {
            if (!LifeStateGuard.IsAlive(npc)) continue;  // WP-3.0.0: skip non-Alive NPCs
            if (!npc.Has<WorkloadComponent>()) continue;

            var wl = npc.Get<WorkloadComponent>();
            var activeTasks = wl.ActiveTasks ?? (IReadOnlyList<Guid>)Array.Empty<Guid>();

            // 1. Recompute load.
            wl.CurrentLoad = wl.Capacity > 0
                ? Math.Clamp(activeTasks.Count * 100 / wl.Capacity, 0, 100)
                : 0;

            // Resolve intended work task (if any).
            Guid workTargetGuid = Guid.Empty;
            if (npc.Has<IntendedActionComponent>())
            {
                var intent = npc.Get<IntendedActionComponent>();
                if (intent.Kind == IntendedActionKind.Work && intent.TargetEntityId != 0)
                {
                    workTargetGuid = FindGuidByIntId(intent.TargetEntityId);
                }
            }

            // Physiology multipliers.
            double physiologyMult = ComputePhysiologyMult(npc);
            double stressMult     = ComputeStressMult(npc);
            double consciousnessMult = 1.0;
            if (npc.Has<PersonalityComponent>())
            {
                var p = npc.Get<PersonalityComponent>();
                consciousnessMult = 1.0 + p.Conscientiousness * _cfg.ConscientiousnessProgressBias;
            }

            bool isGoodCondition =
                physiologyMult >= 0.9 &&
                stressMult     >= 0.9 &&
                !npc.Has<HungryTag>() &&
                !npc.Has<DehydratedTag>();

            // 2. Advance progress / quality on worked task; detect overdue for all tasks.
            var completedGuids = new List<Guid>();
            var newlyOverdueGuids = new List<Guid>();

            foreach (var taskGuid in activeTasks)
            {
                var taskEntity = FindEntityByGuid(taskGuid);
                if (taskEntity == null) continue;
                if (!taskEntity.Has<TaskComponent>()) continue;

                var task = taskEntity.Get<TaskComponent>();

                // Advance progress if NPC is working this task.
                if (taskGuid == workTargetGuid)
                {
                    double rate = _cfg.BaseProgressRatePerSecond
                        * physiologyMult * stressMult * consciousnessMult;

                    task.Progress = Math.Clamp(task.Progress + (float)(rate * deltaTime), 0f, 1f);

                    // Quality update.
                    if (isGoodCondition)
                        task.QualityLevel = Math.Clamp(
                            task.QualityLevel + (float)(_cfg.QualityRecoveryPerGoodTick * deltaTime), 0f, 1f);
                    else
                        task.QualityLevel = Math.Clamp(
                            task.QualityLevel - (float)(_cfg.QualityDecayPerStressedTick * deltaTime), 0f, 1f);

                    taskEntity.Add(task);
                }

                // Check completion.
                if (taskEntity.Get<TaskComponent>().Progress >= 1.0f)
                {
                    completedGuids.Add(taskGuid);
                    EmitCandidate(NarrativeEventKind.TaskCompleted,
                        WillpowerSystem.EntityIntId(npc), task);
                    continue;
                }

                // Check newly overdue (OverdueTag not yet present).
                if (now > task.DeadlineTick && !taskEntity.Has<OverdueTag>())
                {
                    taskEntity.Add(new OverdueTag());
                    newlyOverdueGuids.Add(taskGuid);
                    EmitCandidate(NarrativeEventKind.OverdueTask,
                        WillpowerSystem.EntityIntId(npc), task);
                }
            }

            // 3. Remove completed tasks.
            if (completedGuids.Count > 0)
            {
                var remaining = new List<Guid>(activeTasks.Count);
                foreach (var g in activeTasks)
                {
                    if (completedGuids.Contains(g))
                    {
                        var te = FindEntityByGuid(g);
                        if (te != null) em.DestroyEntity(te);
                    }
                    else
                    {
                        remaining.Add(g);
                    }
                }
                activeTasks = remaining;
                wl.ActiveTasks = remaining;
                wl.CurrentLoad = wl.Capacity > 0
                    ? Math.Clamp(remaining.Count * 100 / wl.Capacity, 0, 100)
                    : 0;
            }

            npc.Add(wl);
        }
    }

    // ── Multiplier helpers ────────────────────────────────────────────────────

    private static double ComputePhysiologyMult(Entity npc)
    {
        double m = 1.0;
        if (npc.Has<EnergyComponent>())
            m *= npc.Get<EnergyComponent>().Energy / 100.0;
        if (npc.Has<HungryTag>())    m *= 0.7;
        if (npc.Has<DehydratedTag>()) m *= 0.6;
        if (npc.Has<BladderCriticalTag>()) m *= 0.3;
        return Math.Max(0.0, m);
    }

    private static double ComputeStressMult(Entity npc)
    {
        if (npc.Has<OverwhelmedTag>()) return 0.5;
        if (npc.Has<StressedTag>())    return 0.8;
        return 1.0;
    }

    // ── Entity lookup helpers ─────────────────────────────────────────────────

    private Entity? FindEntityByGuid(Guid guid)
    {
        foreach (var e in _em.GetAllEntities())
            if (e.Id == guid) return e;
        return null;
    }

    private Guid FindGuidByIntId(int intId)
    {
        foreach (var e in _em.GetAllEntities())
            if (WillpowerSystem.EntityIntId(e) == intId) return e.Id;
        return Guid.Empty;
    }

    // ── Narrative emission ────────────────────────────────────────────────────

    private void EmitCandidate(NarrativeEventKind kind, int npcIntId, TaskComponent task)
    {
        _bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           (long)_clock.TotalTime,
            Kind:           kind,
            ParticipantIds: new[] { npcIntId },
            RoomId:         null,
            Detail:         $"{kind} priority={task.Priority} progress={task.Progress:F2}"
        ));
    }
}
