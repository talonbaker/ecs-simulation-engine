using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Manages the Alive → Incapacitated → Deceased state machine.
/// THE ONLY WRITER of LifeStateComponent.State and the only attacher of CauseOfDeathComponent.
///
/// Producers (choking scenario, slip-and-fall, starvation) call RequestTransition.
/// The system drains the request queue each Cleanup tick in deterministic order.
/// When transitioning to Deceased, emits a cause-of-death narrative event BEFORE state flips,
/// allowing subscribers to see the deceased while still flagged as Alive.
/// </summary>
public class LifeStateTransitionSystem : ISystem
{
    private readonly List<LifeStateTransitionRequest> _queue = new();
    private readonly NarrativeEventBus _narrativeEventBus;
    private readonly EntityManager _entityManager;
    private readonly SimulationClock _clock;
    private readonly SimConfig _config;

    public LifeStateTransitionSystem(
        NarrativeEventBus narrativeEventBus,
        EntityManager entityManager,
        SimulationClock clock,
        SimConfig config)
    {
        _narrativeEventBus = narrativeEventBus ?? throw new ArgumentNullException(nameof(narrativeEventBus));
        _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Enqueue a life-state transition request.
    /// If multiple requests target the same NPC in the same tick, later requests win (deterministic warning logged).
    /// The queue is drained each Cleanup tick in ascending NpcId order.
    /// </summary>
    public void RequestTransition(Guid npcId, Components.LifeState targetState, CauseOfDeath cause)
    {
        // Dedupe by npcId; later requests in the same tick win.
        var existing = _queue.FirstOrDefault(r => r.NpcId == npcId);
        if (existing != null)
        {
            _queue.Remove(existing);
        }

        _queue.Add(new LifeStateTransitionRequest(npcId, targetState, cause));
    }

    public void Update(EntityManager em, float deltaTime)
    {
        // Process queue in deterministic order: ascending NpcId.
        var orderedRequests = _queue.OrderBy(r => r.NpcId).ToList();

        foreach (var req in orderedRequests)
        {
            var npc = em.Query<NpcTag>().FirstOrDefault(e => e.Id == req.NpcId);
            if (npc == null) continue;
            if (!npc.Has<LifeStateComponent>()) continue;

            var current = npc.Get<LifeStateComponent>().State;

            // Legal transitions: Alive → Incapacitated → Deceased; Alive → Deceased (sudden death)
            if (current == Components.LifeState.Deceased) continue;  // already dead, ignore request
            if (req.TargetState == Components.LifeState.Alive && current == Components.LifeState.Deceased) continue;  // no resurrection

            // Emit cause-of-death narrative BEFORE flipping state to Deceased,
            // so subscribers see the deceased while still flagged as Alive in their entity snapshot.
            if (req.TargetState == Components.LifeState.Deceased)
            {
                var witness = FindClosestWitness(npc, em);
                // Location room ID: populated by future packets (3.0.2+).
                // For now, we just store the position and later packets will resolve room membership.
                var location = Guid.Empty;

                var participantIds = new List<int> { GetEntityIntId(npc) };
                if (witness != Guid.Empty)
                {
                    var witnessEnt = em.Query<NpcTag>().FirstOrDefault(e => e.Id == witness);
                    if (witnessEnt != null)
                        participantIds.Add(GetEntityIntId(witnessEnt));
                }

                _narrativeEventBus.RaiseCandidate(new Narrative.NarrativeEventCandidate(
                    (long)_clock.TotalTime,
                    CauseToNarrativeKind(req.Cause),
                    participantIds,
                    location.ToString(),
                    $"Death from {req.Cause}"
                ));

                // Attach the cause-of-death record.
                npc.Add(new CauseOfDeathComponent
                {
                    Cause = req.Cause,
                    DeathTick = (long)_clock.TotalTime,
                    WitnessedByNpcId = witness,
                    LocationRoomId = location
                });
            }

            // Update state.
            npc.Add(new LifeStateComponent
            {
                State = req.TargetState,
                LastTransitionTick = (long)_clock.TotalTime,
                IncapacitatedTickBudget = req.TargetState == Components.LifeState.Incapacitated
                    ? _config.LifeState.DefaultIncapacitatedTicks
                    : 0,
                PendingDeathCause = req.TargetState == Components.LifeState.Incapacitated ? req.Cause : CauseOfDeath.Unknown
            });
        }

        _queue.Clear();

        // Separately: tick down IncapacitatedTickBudget for any Incapacitated NPC.
        // When it reaches 0, enqueue a Deceased transition with the pending cause.
        foreach (var npc in em.Query<NpcTag>())
        {
            if (!npc.Has<LifeStateComponent>()) continue;
            var state = npc.Get<LifeStateComponent>();
            if (state.State != Components.LifeState.Incapacitated) continue;

            // Decrement the budget.
            state.IncapacitatedTickBudget--;
            npc.Add(state);

            // If budget expired, queue a Deceased transition.
            if (state.IncapacitatedTickBudget <= 0)
            {
                RequestTransition(npc.Id, Components.LifeState.Deceased, state.PendingDeathCause);
            }
        }

        // Re-process the newly-enqueued Deceased requests in this same tick.
        // This ensures the transition happens immediately when the budget expires,
        // so the NPC is visibly dead by the next Narrative phase.
        if (_queue.Count > 0)
        {
            Update(em, deltaTime);  // recurse once to drain newly-enqueued requests
        }
    }

    /// <summary>
    /// Find the first NPC in conversation range of the deceased.
    /// Only alive NPCs can witness (Incapacitated cannot form memory; Deceased are already gone).
    /// Returns in deterministic order (ascending EntityIntId).
    /// </summary>
    private Guid FindClosestWitness(Entity deceased, EntityManager em)
    {
        if (!deceased.Has<ProximityComponent>()) return Guid.Empty;

        var conversationRange = deceased.Get<ProximityComponent>().ConversationRangeTiles;
        var witnesses = new List<Guid>();

        // Iterate all NPCs in conversation range.
        foreach (var npc in em.Query<NpcTag>())
        {
            if (npc.Id == deceased.Id) continue;  // skip self
            if (!npc.Has<LifeStateComponent>()) continue;
            if (!LifeStateGuard.IsAlive(npc)) continue;  // only alive NPCs can witness
            if (!npc.Has<PositionComponent>() || !deceased.Has<PositionComponent>()) continue;

            var npcPos = npc.Get<PositionComponent>();
            var deceasedPos = deceased.Get<PositionComponent>();
            float dist = MathF.Sqrt(
                MathF.Pow(npcPos.X - deceasedPos.X, 2) +
                MathF.Pow(npcPos.Z - deceasedPos.Z, 2)
            );

            if (dist <= conversationRange)
                witnesses.Add(npc.Id);
        }

        // Return in deterministic order (ascending EntityIntId).
        if (witnesses.Count == 0) return Guid.Empty;
        witnesses.Sort((a, b) =>
        {
            var aEnt = em.Query<NpcTag>().FirstOrDefault(e => e.Id == a);
            var bEnt = em.Query<NpcTag>().FirstOrDefault(e => e.Id == b);
            if (aEnt == null || bEnt == null) return 0;
            return GetEntityIntId(aEnt).CompareTo(GetEntityIntId(bEnt));
        });

        return witnesses[0];
    }

    /// <summary>Extract the integer ID from entity's Guid (stored in bytes 0-7 little-endian).</summary>
    private static int GetEntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return BitConverter.ToInt32(b, 0);
    }

    /// <summary>Map CauseOfDeath to the corresponding NarrativeEventKind.</summary>
    private static Narrative.NarrativeEventKind CauseToNarrativeKind(CauseOfDeath cause) => cause switch
    {
        CauseOfDeath.Choked => Narrative.NarrativeEventKind.Choked,
        CauseOfDeath.SlippedAndFell => Narrative.NarrativeEventKind.SlippedAndFell,
        CauseOfDeath.StarvedAlone => Narrative.NarrativeEventKind.StarvedAlone,
        _ => Narrative.NarrativeEventKind.Died,
    };
}
