using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Narrative;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Per-tick system that watches for fainted NPCs whose recovery window has elapsed
/// and queues the transition back to <see cref="LifeState.Alive"/>.
///
/// RECOVERY CONTRACT
/// ─────────────────
/// For each Incapacitated NPC with <see cref="IsFaintingTag"/>:
///   1. If <c>clock.CurrentTick &gt;= FaintingComponent.RecoveryTick</c>:
///      a. Optionally emits <see cref="NarrativeEventKind.RegainedConsciousness"/>
///         on the <see cref="NarrativeEventBus"/> (before the state flip, so the
///         participant is still technically Incapacitated when the event fires —
///         consistent with the "emit before flip" contract of WP-3.0.0).
///      b. Calls <see cref="LifeStateTransitionSystem.RequestTransition"/> with
///         <see cref="LifeState.Alive"/> and <see cref="CauseOfDeath.Unknown"/>.
///         The existing <c>case LifeState.Alive</c> branch in
///         <see cref="LifeStateTransitionSystem.ApplyRequest"/> handles this —
///         it was stubbed as the "rescue mechanic" in WP-3.0.0.
///
/// PHASE ORDERING
/// ──────────────
/// Cleanup phase, registered BEFORE <see cref="LifeStateTransitionSystem"/> so the
/// recovery request is drained and applied in the same tick it is queued.
/// <see cref="LifeStateTransitionSystem"/> processes the recovery queue (step 1 of its
/// Update) before the budget-expiry death check (step 2), so fainting can never kill.
///
/// Deterministic: fainted NPCs iterated in ascending EntityIntId order.
///
/// WP-3.0.6: Fainting System.
/// </summary>
public sealed class FaintingRecoverySystem : ISystem
{
    private readonly LifeStateTransitionSystem _transition;
    private readonly NarrativeEventBus         _narrativeBus;
    private readonly SimulationClock           _clock;
    private readonly FaintingConfig            _cfg;

    public FaintingRecoverySystem(
        LifeStateTransitionSystem transition,
        NarrativeEventBus         narrativeBus,
        SimulationClock           clock,
        FaintingConfig            cfg)
    {
        _transition   = transition;
        _narrativeBus = narrativeBus;
        _clock        = clock;
        _cfg          = cfg;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var npc in em.Query<IsFaintingTag>()
                               .OrderBy(e => EntityIntId(e))
                               .ToList())
        {
            // Only process Incapacitated fainted NPCs (not ones already recovered).
            if (!npc.Has<LifeStateComponent>()) continue;
            if (npc.Get<LifeStateComponent>().State != LifeState.Incapacitated) continue;

            if (!npc.Has<FaintingComponent>()) continue;
            var faint = npc.Get<FaintingComponent>();

            // Recovery tick not yet reached — keep waiting.
            if (_clock.CurrentTick < faint.RecoveryTick) continue;

            // ── Recovery triggered ────────────────────────────────────────────

            // Step 1: Optionally emit RegainedConsciousness narrative BEFORE state flip.
            if (_cfg.EmitRegainedConsciousnessNarrative)
            {
                _narrativeBus.RaiseCandidate(new NarrativeEventCandidate(
                    Tick:           _clock.CurrentTick,
                    Kind:           NarrativeEventKind.RegainedConsciousness,
                    ParticipantIds: new[] { EntityIntId(npc) },
                    RoomId:         null,
                    Detail:         $"NPC {npc.Id} regained consciousness after fainting."
                ));
            }

            // Step 2: Enqueue Alive recovery. LifeStateTransitionSystem.ApplyRequest
            // handles the Alive case from Incapacitated (the WP-3.0.0 rescue mechanic stub).
            _transition.RequestTransition(
                npc.Id,
                LifeState.Alive,
                CauseOfDeath.Unknown);
        }
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }
}
