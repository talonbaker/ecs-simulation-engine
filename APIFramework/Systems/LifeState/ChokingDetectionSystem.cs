using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Detects when an NPC is at risk of choking on a food bolus currently in
/// esophageal transit, and fires the incapacitation pipeline.
///
/// DETECTION CONTRACT
/// ──────────────────
/// All three conditions must hold simultaneously:
///   1. A bolus with BolusComponent.Toughness ≥ BolusSizeThreshold is in transit
///      toward this NPC (EsophagusTransitComponent.TargetEntityId matches the NPC).
///   2. The NPC is Alive and does not already have IsChokingTag.
///   3. At least one distraction condition is true:
///        EnergyComponent.Energy < EnergyThreshold
///        OR StressComponent.AcuteLevel >= StressThreshold
///        OR SocialDrivesComponent.Irritation.Current >= IrritationThreshold.
///
/// ON CHOKE DETECTION
/// ──────────────────
///   1. IsChokingTag is attached to the NPC.
///   2. ChokingComponent is attached (mirrors the tick budget and bolus toughness).
///   3. MoodComponent.PanicLevel is set to ChokingConfig.PanicMoodIntensity.
///   4. Optionally a ChokeStarted narrative candidate is raised on the NarrativeEventBus
///      so that witnesses record the onset of the episode (not just the death).
///   5. LifeStateTransitionSystem.RequestTransition is called with
///      (Incapacitated, Choked, incapacitationTicksOverride = ChokingConfig.IncapacitationTicks).
///
/// PHASE ORDERING
/// ──────────────
/// Cleanup phase, registered BEFORE LifeStateTransitionSystem so the incapacitation
/// request is drained and applied in the same tick the choke is detected.
/// ChokingCleanupSystem runs AFTER LifeStateTransitionSystem to remove the tag once dead.
///
/// WP-3.0.1: Choking-on-Food Scenario.
/// </summary>
/// <seealso cref="ChokingCleanupSystem"/>
/// <seealso cref="LifeStateTransitionSystem"/>
public sealed class ChokingDetectionSystem : ISystem
{
    private readonly LifeStateTransitionSystem _transition;
    private readonly NarrativeEventBus         _narrativeBus;
    private readonly EntityManager             _em;
    private readonly SimulationClock           _clock;
    private readonly EntityRoomMembership      _roomMembership;
    private readonly ChokingConfig             _cfg;

    /// <summary>
    /// Stores the dependencies used per tick.
    /// </summary>
    /// <param name="transition">Single-writer life-state transition system that drains choke incapacitation requests.</param>
    /// <param name="narrativeBus">Bus on which ChokeStarted candidates are emitted before the state flip.</param>
    /// <param name="em">Entity manager — used to resolve transit-target NPCs and witness candidates.</param>
    /// <param name="clock">Simulation clock — supplies <c>CurrentTick</c>.</param>
    /// <param name="roomMembership">Room-membership lookup used to stamp <c>RoomId</c> on emitted candidates.</param>
    /// <param name="cfg">Choking config — bolus and distraction thresholds, panic intensity, narrative toggle.</param>
    public ChokingDetectionSystem(
        LifeStateTransitionSystem transition,
        NarrativeEventBus         narrativeBus,
        EntityManager             em,
        SimulationClock           clock,
        EntityRoomMembership      roomMembership,
        ChokingConfig             cfg)
    {
        _transition     = transition;
        _narrativeBus   = narrativeBus;
        _em             = em;
        _clock          = clock;
        _roomMembership = roomMembership;
        _cfg            = cfg;
    }

    /// <summary>
    /// Per-tick entry point. Walks every esophageal transit and triggers the choking pipeline
    /// for any NPC whose distraction conditions hold while a tough bolus is in transit.
    /// </summary>
    /// <param name="em">Entity manager — queried for boluses and consumer NPCs.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        // Iterate food boluses currently in esophageal transit.
        foreach (var bolus in em.Query<EsophagusTransitComponent>().ToList())
        {
            if (!bolus.Has<BolusComponent>()) continue;
            var bolusData = bolus.Get<BolusComponent>();

            // 1. Only tough boluses can trigger choking.
            if (bolusData.Toughness < _cfg.BolusSizeThreshold) continue;

            // 2. Find the consumer NPC via the transit target reference.
            var transit = bolus.Get<EsophagusTransitComponent>();
            var npc     = FindEntityByGuid(em, transit.TargetEntityId);
            if (npc is null)                  continue;
            if (!LifeStateGuard.IsAlive(npc)) continue; // must be Alive
            if (npc.Has<IsChokingTag>())      continue; // already choking — don't re-trigger

            // 3. Distraction check — at least one physiological/psychological condition must hold.
            bool distracted = false;

            if (npc.Has<EnergyComponent>())
                distracted |= npc.Get<EnergyComponent>().Energy < _cfg.EnergyThreshold;

            if (!distracted && npc.Has<StressComponent>())
                distracted |= npc.Get<StressComponent>().AcuteLevel >= _cfg.StressThreshold;

            if (!distracted && npc.Has<SocialDrivesComponent>())
                distracted |= npc.Get<SocialDrivesComponent>().Irritation.Current >= _cfg.IrritationThreshold;

            if (!distracted) continue;

            // ── Choke triggered ──────────────────────────────────────────────────

            // Step 4: Attach IsChokingTag so other systems can gate on it.
            npc.Add(new IsChokingTag());

            // Step 5: Attach ChokingComponent — mirrors IncapacitatedTickBudget for convenient
            // querying by future systems (rescue mechanic, UI animations, witness reactions).
            npc.Add(new ChokingComponent
            {
                ChokeStartTick = _clock.CurrentTick,
                RemainingTicks = _cfg.IncapacitationTicks,
                BolusSize      = bolusData.Toughness,
                PendingCause   = CauseOfDeath.Choked,
            });

            // Step 6: Spike MoodComponent.PanicLevel (0..1 scale; decays each tick via MoodSystem).
            if (npc.Has<MoodComponent>())
            {
                var mood        = npc.Get<MoodComponent>();
                mood.PanicLevel = _cfg.PanicMoodIntensity;
                npc.Add(mood);
            }

            // Step 7: Optionally emit ChokeStarted narrative candidate before incapacitation so
            // MemoryRecordingSystem records the onset of the episode while participants are Alive.
            if (_cfg.EmitChokeStartedNarrative)
            {
                int?   witnessIntId = FindClosestWitnessIntId(npc);
                string? locationId  = GetRoomId(npc);

                int[] participants = witnessIntId.HasValue
                    ? new[] { EntityIntId(npc), witnessIntId.Value }
                    : new[] { EntityIntId(npc) };

                _narrativeBus.RaiseCandidate(new NarrativeEventCandidate(
                    Tick:           _clock.CurrentTick,
                    Kind:           NarrativeEventKind.ChokeStarted,
                    ParticipantIds: participants,
                    RoomId:         locationId,
                    Detail:         $"NPC {npc.Id} began choking on {bolusData.FoodType} (toughness {bolusData.Toughness:F2})."
                ));
            }

            // Step 8: Enqueue incapacitation with per-cause tick budget.
            // LifeStateTransitionSystem drains this queue later in the same Cleanup tick.
            _transition.RequestTransition(
                npc.Id,
                LifeState.Incapacitated,
                CauseOfDeath.Choked,
                _cfg.IncapacitationTicks);
        }
    }

    // ── Witness selection ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the EntityIntId of the nearest Alive NPC within the choking NPC's
    /// conversation range, or null if none are nearby. Deterministic: smallest
    /// EntityIntId wins on tie — consistent across identical runs.
    /// </summary>
    private int? FindClosestWitnessIntId(Entity choking)
    {
        if (!choking.Has<PositionComponent>()) return null;

        var pos   = choking.Get<PositionComponent>();
        int range = choking.Has<ProximityComponent>()
            ? choking.Get<ProximityComponent>().ConversationRangeTiles
            : ProximityComponent.Default.ConversationRangeTiles;

        int? bestIntId = null;
        int  lowestId  = int.MaxValue;

        foreach (var candidate in _em.Query<NpcTag>().ToList())
        {
            if (candidate.Id == choking.Id)       continue;
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

    private static Entity? FindEntityByGuid(EntityManager em, Guid id)
    {
        foreach (var e in em.GetAllEntities())
        {
            if (e.Id == id) return e;
        }
        return null;
    }

    private string? GetRoomId(Entity entity)
    {
        var room = _roomMembership.GetRoom(entity);
        if (room is null)                    return null;
        if (!room.Has<RoomComponent>())      return null;
        return room.Get<RoomComponent>().Id;
    }

    private static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }
}
