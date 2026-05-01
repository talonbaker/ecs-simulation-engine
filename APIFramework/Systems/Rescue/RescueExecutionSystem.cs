using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Systems.Rescue;

/// <summary>
/// Executes Rescue intents set by <see cref="RescueIntentSystem"/> in the same Cleanup tick.
///
/// For each NPC whose IntendedAction is Rescue and who has reached conversation range of the victim:
///   1. Determine the rescue kind (Heimlich / CPR / DoorUnlock).
///   2. Roll a success check against base rate + archetype competence bonus.
///   3. On success: clear the incapacitating condition, request Alive transition, emit
///      RescuePerformed (persistent) + strong relationship bond update.
///   4. On failure: emit RescueAttempted (non-persistent).
///
/// Victim still dies on failure — LifeStateTransitionSystem's countdown is unaffected.
/// RescueFailed (persistent) is emitted when failure is detected and the victim has no
/// remaining tick budget (i.e., next tick will be Deceased).
///
/// Determinism: iterates rescuers in ascending entity-Id order; uses SeededRandom with no
/// per-call seed — determinism is maintained by the consistent call ordering.
/// </summary>
/// <seealso cref="RescueIntentSystem"/>
public sealed class RescueExecutionSystem : ISystem
{
    private readonly ArchetypeRescueBiasCatalog _catalog;
    private readonly RescueConfig _cfg;
    private readonly LifeStateTransitionSystem _transitions;
    private readonly IWorldMutationApi _mutationApi;
    private readonly NarrativeEventBus _narrativeBus;
    private readonly SimulationClock _clock;
    private readonly SeededRandom _rng;

    public RescueExecutionSystem(
        ArchetypeRescueBiasCatalog catalog,
        RescueConfig cfg,
        LifeStateTransitionSystem transitions,
        IWorldMutationApi mutationApi,
        NarrativeEventBus narrativeBus,
        SimulationClock clock,
        SeededRandom rng)
    {
        _catalog      = catalog      ?? throw new ArgumentNullException(nameof(catalog));
        _cfg          = cfg          ?? throw new ArgumentNullException(nameof(cfg));
        _transitions  = transitions  ?? throw new ArgumentNullException(nameof(transitions));
        _mutationApi  = mutationApi  ?? throw new ArgumentNullException(nameof(mutationApi));
        _narrativeBus = narrativeBus ?? throw new ArgumentNullException(nameof(narrativeBus));
        _clock        = clock        ?? throw new ArgumentNullException(nameof(clock));
        _rng          = rng          ?? throw new ArgumentNullException(nameof(rng));
    }

    public void Update(EntityManager em, float deltaTime)
    {
        var rescuers = em.Query<NpcTag>()
            .Where(e => e.Has<IntendedActionComponent>() &&
                        e.Get<IntendedActionComponent>().Kind == IntendedActionKind.Rescue)
            .OrderBy(e => e.Id)
            .ToList();

        foreach (var rescuer in rescuers)
        {
            var intent = rescuer.Get<IntendedActionComponent>();
            int targetIntId = intent.TargetEntityId;

            var victim = FindEntityByIntId(targetIntId, em);
            if (victim == null) continue;

            // Victim must still be Incapacitated; skip if already rescued or dead.
            if (!victim.Has<LifeStateComponent>()) continue;
            var victimState = victim.Get<LifeStateComponent>().State;
            if (victimState != LS.Incapacitated) continue;

            // Must be in conversation range to execute.
            if (!InConversationRange(rescuer, victim)) continue;

            var kind       = DetermineRescueKind(victim);
            float pSuccess = ComputeSuccessProbability(rescuer, kind);
            float roll     = _rng.NextFloat();

            if (roll < pSuccess)
                PerformRescue(rescuer, victim, kind, em);
            else
                EmitRescueAttemptFailed(rescuer, victim, kind, em);
        }
    }

    // ── Rescue kind ──────────────────────────────────────────────────────────

    private static RescueKind DetermineRescueKind(Entity victim)
    {
        if (victim.Has<IsChokingTag>())      return RescueKind.Heimlich;
        if (victim.Has<LockedInComponent>()) return RescueKind.DoorUnlock;
        return RescueKind.CPR;
    }

    // ── Success probability ──────────────────────────────────────────────────

    private float ComputeSuccessProbability(Entity rescuer, RescueKind kind)
    {
        float baseRate = kind switch
        {
            RescueKind.Heimlich   => _cfg.HeimlichBaseSuccessRate,
            RescueKind.CPR        => _cfg.CprBaseSuccessRate,
            RescueKind.DoorUnlock => _cfg.DoorUnlockBaseSuccessRate,
            _                     => 0.50f,
        };

        string archetypeId = rescuer.Has<NpcArchetypeComponent>()
            ? rescuer.Get<NpcArchetypeComponent>().ArchetypeId
            : string.Empty;

        float bonus = _catalog.GetCompetence(archetypeId, kind);
        return MathF.Min(baseRate + bonus, 0.99f);
    }

    // ── Execution ────────────────────────────────────────────────────────────

    private void PerformRescue(Entity rescuer, Entity victim, RescueKind kind, EntityManager em)
    {
        switch (kind)
        {
            case RescueKind.Heimlich:
                if (victim.Has<IsChokingTag>())     victim.Remove<IsChokingTag>();
                if (victim.Has<ChokingComponent>()) victim.Remove<ChokingComponent>();
                _transitions.RequestTransition(victim.Id, LS.Alive, CauseOfDeath.Unknown);
                break;

            case RescueKind.CPR:
                _transitions.RequestTransition(victim.Id, LS.Alive, CauseOfDeath.Unknown);
                break;

            case RescueKind.DoorUnlock:
                var door = FindLockedDoorForVictim(victim, em);
                if (door != null)
                    _mutationApi.DetachObstacle(door.Id);
                if (victim.Has<LockedInComponent>())
                    victim.Remove<LockedInComponent>();
                _transitions.RequestTransition(victim.Id, LS.Alive, CauseOfDeath.Unknown);
                break;
        }

        _narrativeBus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           (long)_clock.TotalTime,
            Kind:           NarrativeEventKind.RescuePerformed,
            ParticipantIds: new[] { EntityIntId(victim), EntityIntId(rescuer) },
            RoomId:         null,
            Detail:         $"rescue:{kind.ToString().ToLowerInvariant()}"
        ));
    }

    private void EmitRescueAttemptFailed(Entity rescuer, Entity victim, RescueKind kind, EntityManager em)
    {
        // Check if the victim's budget is at zero — if so, they are about to die: emit RescueFailed.
        bool aboutToDie = victim.Has<LifeStateComponent>() &&
                          victim.Get<LifeStateComponent>().IncapacitatedTickBudget <= 0;

        var narrativeKind = aboutToDie
            ? NarrativeEventKind.RescueFailed
            : NarrativeEventKind.RescueAttempted;

        _narrativeBus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           (long)_clock.TotalTime,
            Kind:           narrativeKind,
            ParticipantIds: new[] { EntityIntId(victim), EntityIntId(rescuer) },
            RoomId:         null,
            Detail:         $"rescue-failed:{kind.ToString().ToLowerInvariant()}"
        ));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool InConversationRange(Entity rescuer, Entity victim)
    {
        if (!rescuer.Has<PositionComponent>() || !victim.Has<PositionComponent>()) return false;
        int range = rescuer.Has<ProximityComponent>()
            ? rescuer.Get<ProximityComponent>().ConversationRangeTiles
            : 2;

        var rp = rescuer.Get<PositionComponent>();
        var vp = victim.Get<PositionComponent>();
        float dist = MathF.Sqrt(MathF.Pow(rp.X - vp.X, 2) + MathF.Pow(rp.Z - vp.Z, 2));
        return dist <= range;
    }

    /// <summary>
    /// Finds the nearest ObstacleTag entity to the victim — v0.1 proxy for the door blocking their exit.
    /// Returns null when no obstacle entities are present.
    /// </summary>
    private static Entity? FindLockedDoorForVictim(Entity victim, EntityManager em)
    {
        if (!victim.Has<PositionComponent>()) return null;

        var victimPos = victim.Get<PositionComponent>();
        Entity? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var entity in em.Query<ObstacleTag>())
        {
            if (!entity.Has<PositionComponent>()) continue;
            var ep = entity.Get<PositionComponent>();
            float d = MathF.Sqrt(MathF.Pow(ep.X - victimPos.X, 2) + MathF.Pow(ep.Z - victimPos.Z, 2));
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest     = entity;
            }
        }

        return nearest;
    }

    private static Entity? FindEntityByIntId(int intId, EntityManager em)
    {
        foreach (var e in em.Query<NpcTag>())
        {
            if (EntityIntId(e) == intId) return e;
        }
        return null;
    }

    private static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return BitConverter.ToInt32(b, 0);
    }
}
