using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Detects when an NPC's fear has reached the level at which they lose consciousness,
/// and fires the incapacitation pipeline for a temporary, non-fatal faint.
///
/// DETECTION CONTRACT
/// ──────────────────
/// All three conditions must hold simultaneously:
///   1. The NPC is <see cref="LifeState.Alive"/> (not already incapacitated or dead).
///   2. The NPC does not already have <see cref="IsFaintingTag"/> (idempotent guard).
///   3. <see cref="MoodComponent.Fear"/> &gt;= <see cref="FaintingConfig.FearThreshold"/>.
///
/// ON FAINT DETECTION
/// ──────────────────
///   1. <see cref="IsFaintingTag"/> is attached.
///   2. <see cref="FaintingComponent"/> is attached with <c>FaintStartTick</c> and
///      <c>RecoveryTick = currentTick + FaintDurationTicks</c>.
///   3. Optionally a <see cref="NarrativeEventKind.Fainted"/> candidate is raised on the
///      <see cref="NarrativeEventBus"/> — emitted BEFORE the incapacitation request is
///      enqueued so <see cref="MemoryRecordingSystem"/> sees an Alive participant
///      (per the WP-3.0.0 narrative-emit contract).
///   4. <see cref="LifeStateTransitionSystem.RequestTransition"/> is called with
///      (<see cref="LifeState.Incapacitated"/>, <see cref="CauseOfDeath.Unknown"/>,
///      incapacitationTicksOverride = <c>FaintDurationTicks + 1</c>).
///      The +1 budget ensures the death-by-budget-expiry check cannot fire before
///      <see cref="FaintingRecoverySystem"/> queues recovery.
///
/// FAINTING IS NEVER FATAL
/// ───────────────────────
/// The <c>CauseOfDeath.Unknown</c> cause and the +1 budget guarantee that the NPC
/// will always recover via <see cref="FaintingRecoverySystem"/> before
/// <see cref="LifeStateTransitionSystem"/> could auto-promote them to Deceased.
///
/// PHASE ORDERING
/// ──────────────
/// Cleanup phase, registered BEFORE <see cref="LifeStateTransitionSystem"/> so the
/// incapacitation request is drained and applied in the same tick the faint is detected.
/// <see cref="FaintingCleanupSystem"/> runs AFTER <see cref="LifeStateTransitionSystem"/>
/// to strip tags from recovered (Alive) NPCs.
///
/// Deterministic: multiple fainting NPCs in the same tick are iterated in ascending
/// EntityIntId order.
///
/// WP-3.0.6: Fainting System.
/// </summary>
public sealed class FaintingDetectionSystem : ISystem
{
    private readonly LifeStateTransitionSystem _transition;
    private readonly NarrativeEventBus         _narrativeBus;
    private readonly SimulationClock           _clock;
    private readonly EntityRoomMembership      _roomMembership;
    private readonly FaintingConfig            _cfg;

    public FaintingDetectionSystem(
        LifeStateTransitionSystem transition,
        NarrativeEventBus         narrativeBus,
        SimulationClock           clock,
        EntityRoomMembership      roomMembership,
        FaintingConfig            cfg)
    {
        _transition     = transition;
        _narrativeBus   = narrativeBus;
        _clock          = clock;
        _roomMembership = roomMembership;
        _cfg            = cfg;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        // Deterministic iteration order.
        foreach (var npc in em.Query<NpcTag>()
                               .OrderBy(e => EntityIntId(e))
                               .ToList())
        {
            // 1. Must be Alive.
            if (!LifeStateGuard.IsAlive(npc)) continue;

            // 2. Idempotent guard — don't re-trigger if already fainting.
            if (npc.Has<IsFaintingTag>()) continue;

            // 3. Fear must meet the threshold.
            if (!npc.Has<MoodComponent>()) continue;
            if (npc.Get<MoodComponent>().Fear < _cfg.FearThreshold) continue;

            // ── Faint triggered ──────────────────────────────────────────────

            long startTick    = _clock.CurrentTick;
            long recoveryTick = startTick + _cfg.FaintDurationTicks;

            // Step 1: Attach IsFaintingTag.
            npc.Add(new IsFaintingTag());

            // Step 2: Attach FaintingComponent with timing metadata.
            npc.Add(new FaintingComponent
            {
                FaintStartTick = startTick,
                RecoveryTick   = recoveryTick,
            });

            // Step 3: Optionally emit Fainted narrative BEFORE state flip.
            if (_cfg.EmitFaintedNarrative)
            {
                int?   witnessIntId = FindClosestWitnessIntId(em, npc);
                string? locationId  = GetRoomId(npc);

                int[] participants = witnessIntId.HasValue
                    ? new[] { EntityIntId(npc), witnessIntId.Value }
                    : new[] { EntityIntId(npc) };

                _narrativeBus.RaiseCandidate(new NarrativeEventCandidate(
                    Tick:           startTick,
                    Kind:           NarrativeEventKind.Fainted,
                    ParticipantIds: participants,
                    RoomId:         locationId,
                    Detail:         $"NPC {npc.Id} fainted from extreme fear (Fear={npc.Get<MoodComponent>().Fear:F0})."
                ));
            }

            // Step 4: Enqueue incapacitation with FaintDurationTicks + 1 budget.
            // The +1 ensures the death-by-budget check never fires before FaintingRecoverySystem.
            _transition.RequestTransition(
                npc.Id,
                LifeState.Incapacitated,
                CauseOfDeath.Unknown,
                _cfg.FaintDurationTicks + 1);
        }
    }

    // ── Witness selection ─────────────────────────────────────────────────────

    private int? FindClosestWitnessIntId(EntityManager em, Entity fainting)
    {
        if (!fainting.Has<PositionComponent>()) return null;

        var pos   = fainting.Get<PositionComponent>();
        int range = fainting.Has<ProximityComponent>()
            ? fainting.Get<ProximityComponent>().ConversationRangeTiles
            : ProximityComponent.Default.ConversationRangeTiles;

        int? bestIntId = null;
        int  lowestId  = int.MaxValue;

        foreach (var candidate in em.Query<NpcTag>().ToList())
        {
            if (candidate.Id == fainting.Id)        continue;
            if (!LifeStateGuard.IsAlive(candidate)) continue;
            if (!candidate.Has<PositionComponent>()) continue;

            var   cPos = candidate.Get<PositionComponent>();
            float dx   = cPos.X - pos.X;
            float dz   = cPos.Z - pos.Z;
            float dist = MathF.Sqrt(dx * dx + dz * dz);

            if (dist <= range)
            {
                int id = EntityIntId(candidate);
                if (id < lowestId) { lowestId = id; bestIntId = id; }
            }
        }

        return bestIntId;
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private string? GetRoomId(Entity entity)
    {
        var room = _roomMembership.GetRoom(entity);
        if (room is null)               return null;
        if (!room.Has<RoomComponent>()) return null;
        return room.Get<RoomComponent>().Id;
    }

    private static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }
}
