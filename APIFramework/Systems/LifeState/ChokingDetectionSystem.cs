using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Narrative;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Detects choking conditions and triggers the incapacitation sequence.
///
/// Each tick, iterates NPCs with EsophagusTransitComponent and checks if they are
/// choking: bolus size above threshold AND at least one of three distraction conditions
/// (low energy, high stress, high irritation).
///
/// On choke detection:
/// 1. Attaches IsChokingTag and ChokingComponent to the NPC.
/// 2. Sets MoodComponent.PanicLevel to panicMoodIntensity (panic freezes facing).
/// 3. Emits ChokeStarted narrative with the NPC and any witness in conversation range.
/// 4. Calls LifeStateTransitionSystem.RequestTransition(npc, Incapacitated, Choked).
///
/// Deterministic: iterates in OrderBy(e.Id) order. All thresholds are scalar comparisons.
/// Single-shot: IsAlive + !Has{IsChokingTag} guards prevent re-triggering.
/// </summary>
/// <remarks>
/// Phase: <see cref="SystemPhase.Cleanup"/>. Registered before <see cref="LifeStateTransitionSystem"/>
/// in the same phase so the enqueued transition request is drained in this same tick.
/// Reads: <see cref="EsophagusTransitComponent"/>, <see cref="BolusComponent"/>, <see cref="EnergyComponent"/>,
/// <see cref="StressComponent"/>, <see cref="SocialDrivesComponent"/>, <see cref="MoodComponent"/>,
/// <see cref="ProximityComponent"/>, <see cref="PositionComponent"/>, <see cref="LifeStateComponent"/>.
/// Writes: attaches <see cref="IsChokingTag"/> and <see cref="ChokingComponent"/>; mutates
/// <see cref="MoodComponent.PanicLevel"/>; emits a ChokeStarted narrative candidate; enqueues
/// a transition via <see cref="LifeStateTransitionSystem.RequestTransition"/>. Does not write
/// <see cref="LifeStateComponent"/> directly — only <see cref="LifeStateTransitionSystem"/> may.
/// </remarks>
/// <seealso cref="LifeStateTransitionSystem"/>
/// <seealso cref="ChokingCleanupSystem"/>
public class ChokingDetectionSystem : ISystem
{
    private readonly LifeStateTransitionSystem _transition;
    private readonly NarrativeEventBus _narrative;
    private readonly SimulationClock _clock;
    private readonly ChokingConfig _cfg;
    private readonly EntityManager _em;

    /// <summary>
    /// Constructs the choking detection system with all required dependencies.
    /// </summary>
    /// <param name="transition">Life-state transition system; receives Incapacitated requests when a choke fires.</param>
    /// <param name="narrative">Narrative event bus; receives ChokeStarted candidates.</param>
    /// <param name="clock">Simulation clock; supplies the current tick stamped onto markers and events.</param>
    /// <param name="cfg">Choking thresholds and tuning values (bolus size, distraction triggers, panic intensity, incapacitation tick budget).</param>
    /// <param name="em">Entity manager used for cross-entity lookups (bolus by id, witnesses).</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public ChokingDetectionSystem(
        LifeStateTransitionSystem transition,
        NarrativeEventBus narrative,
        SimulationClock clock,
        ChokingConfig cfg,
        EntityManager em)
    {
        _transition = transition ?? throw new ArgumentNullException(nameof(transition));
        _narrative = narrative ?? throw new ArgumentNullException(nameof(narrative));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        _em = em ?? throw new ArgumentNullException(nameof(em));
    }

    /// <summary>
    /// Iterates NPCs in transit, evaluates the choke condition, and on trigger attaches markers,
    /// spikes panic mood, emits a ChokeStarted narrative event, and enqueues an Incapacitated transition.
    /// </summary>
    /// <param name="em">Entity manager (typically the same instance held in this system).</param>
    /// <param name="deltaTime">Tick delta in seconds (unused; the system runs strictly at tick granularity).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var npc in em.Query<EsophagusTransitComponent>().OrderBy(e => e.Id))
        {
            // Early returns: dead, already choking, no bolus in transit
            if (!LifeStateGuard.IsAlive(npc)) continue;
            if (npc.Has<IsChokingTag>()) continue;  // already choking; transition system handles countdown

            var transit = npc.Get<EsophagusTransitComponent>();

            // Compute bolus size. The transit component tracks a bolus entity by ID.
            float bolusSize = 0f;
            if (transit.TargetEntityId != Guid.Empty)
            {
                var bolusEntity = em.GetAllEntities().FirstOrDefault(e => e.Id == transit.TargetEntityId);
                if (bolusEntity != null && bolusEntity.Has<BolusComponent>())
                {
                    var bolus = bolusEntity.Get<BolusComponent>();
                    bolusSize = bolus.Volume;
                }
            }

            // Below threshold: no choke risk
            if (bolusSize < _cfg.BolusSizeThreshold) continue;

            // Distraction check — at least one of three conditions must hold
            bool distracted =
                (npc.Has<EnergyComponent>() && npc.Get<EnergyComponent>().Energy < _cfg.EnergyThreshold)
                || (npc.Has<StressComponent>() && npc.Get<StressComponent>().AcuteLevel >= _cfg.StressThreshold)
                || (npc.Has<SocialDrivesComponent>() && npc.Get<SocialDrivesComponent>().Irritation.Current >= _cfg.IrritationThreshold);

            if (!distracted) continue;

            // -- CHOKE FIRES ----------------------------------------------------------

            // 1. Attach choking markers
            npc.Add(new IsChokingTag());
            npc.Add(new ChokingComponent
            {
                ChokeStartTick = (long)_clock.TotalTime,
                RemainingTicks = _cfg.IncapacitationTicks,
                BolusSize = bolusSize,
                PendingCause = CauseOfDeath.Choked
            });

            // 2. Set panic mood (existing mood system applies decay)
            if (npc.Has<MoodComponent>())
            {
                var mood = npc.Get<MoodComponent>();
                mood.PanicLevel = MathF.Max(mood.PanicLevel, _cfg.PanicMoodIntensity);
                npc.Add(mood);
            }

            // 3. Emit narrative BEFORE transition request (so subscribers see Alive at this instant)
            if (_cfg.EmitChokeStartedNarrative)
            {
                var participants = FindParticipantsWithWitness(npc, em);
                _narrative.RaiseCandidate(new NarrativeEventCandidate(
                    Tick: (long)_clock.TotalTime,
                    Kind: NarrativeEventKind.ChokeStarted,
                    ParticipantIds: participants,
                    RoomId: null,
                    Detail: "started choking"
                ));
            }

            // 4. Enqueue transition to Incapacitated(Choked)
            // The LifeStateTransitionSystem will set IncapacitatedTickBudget = _cfg.IncapacitationTicks
            // and PendingDeathCause = Choked. On budget expiry, it transitions to Deceased(Choked).
            _transition.RequestTransition(npc.Id, Components.LifeState.Incapacitated, CauseOfDeath.Choked);
        }
    }

    /// <summary>
    /// Returns a participant list for the ChokeStarted narrative event.
    /// Includes the choking NPC and, if present, the closest alive NPC in conversation range.
    /// </summary>
    /// <param name="choker">The NPC who has just started choking.</param>
    /// <param name="em">Entity manager used to find alive witnesses.</param>
    /// <returns>An array of entity integer IDs; always contains the choker, plus optionally one witness.</returns>
    private int[] FindParticipantsWithWitness(Entity choker, EntityManager em)
    {
        // Find closest alive NPC in conversation range
        Guid? witness = FindClosestWitness(choker, em);

        // Convert Guid to entity serial number for the participant list
        List<int> participants = new();

        // Add choker
        var chokerIntId = ExtractEntityIntId(choker.Id);
        participants.Add((int)chokerIntId);

        // Add witness if present
        if (witness.HasValue)
        {
            var witnessIntId = ExtractEntityIntId(witness.Value);
            participants.Add((int)witnessIntId);
        }

        return participants.ToArray();
    }

    /// <summary>
    /// Returns the EntityId of the closest alive NPC in conversation range, or null if none.
    /// Deterministic: iterates witnesses in ascending EntityIntId order and returns the first.
    /// </summary>
    /// <param name="choker">The NPC who has just started choking.</param>
    /// <param name="em">Entity manager used to query NPCs.</param>
    /// <returns>The Guid of the chosen witness, or null when no eligible witness is in range.</returns>
    private Guid? FindClosestWitness(Entity choker, EntityManager em)
    {
        if (!choker.Has<ProximityComponent>()) return null;
        if (!choker.Has<PositionComponent>()) return null;

        var conversationRange = choker.Get<ProximityComponent>().ConversationRangeTiles;
        var chokerPos = choker.Get<PositionComponent>();
        var witnesses = new List<Guid>();

        // Iterate all NPCs in conversation range
        foreach (var npc in em.Query<NpcTag>())
        {
            if (npc.Id == choker.Id) continue;  // skip self
            if (!LifeStateGuard.IsAlive(npc)) continue;  // only alive NPCs can witness
            if (!npc.Has<PositionComponent>()) continue;

            var npcPos = npc.Get<PositionComponent>();
            float dist = MathF.Sqrt(
                MathF.Pow(npcPos.X - chokerPos.X, 2) +
                MathF.Pow(npcPos.Z - chokerPos.Z, 2)
            );

            if (dist <= conversationRange)
                witnesses.Add(npc.Id);
        }

        // Return in deterministic order (ascending EntityIntId)
        if (witnesses.Count == 0) return null;

        witnesses.Sort((a, b) =>
        {
            var aIntId = ExtractEntityIntId(a);
            var bIntId = ExtractEntityIntId(b);
            return aIntId.CompareTo(bIntId);
        });

        return witnesses[0];
    }

    /// <summary>
    /// Extracts the entity's internal integer ID from its Guid for deterministic ordering.
    /// Matches the pattern used in WillpowerSystem.
    /// </summary>
    /// <param name="id">The Guid to extract the integer ID from.</param>
    /// <returns>The first 8 bytes of the Guid interpreted as a little-endian Int64.</returns>
    private static long ExtractEntityIntId(Guid id)
    {
        var bytes = id.ToByteArray();
        return BitConverter.ToInt64(bytes, 0);
    }
}
