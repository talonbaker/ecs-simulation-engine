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
/// Owns the <see cref="LifeState"/> state machine. This is the ONLY writer of
/// <see cref="LifeStateComponent.State"/> and the ONLY attacher of
/// <see cref="CauseOfDeathComponent"/>.
///
/// Producers (choking scenario, slip-and-fall, etc.) call
/// <see cref="RequestTransition"/> to push a request into the queue.
/// The queue is drained once per tick, in ascending NpcId order, to ensure
/// deterministic behaviour when multiple NPCs transition in the same tick.
///
/// Phase: Cleanup — after WorkloadSystem, MaskCrackSystem, and StressSystem so
/// all cognitive state has settled before the NPC is declared dead.
///
/// Narrative emit contract:
/// The cause-of-death narrative candidate is emitted BEFORE the state flips to
/// <see cref="LifeState.Deceased"/> so that <see cref="MemoryRecordingSystem"/>
/// (which subscribes to the bus synchronously) sees participants as still Alive
/// when routing the memory entry. This is intentional and must not be changed.
/// </summary>
/// <remarks>
/// SINGLE-WRITER RULE: this system is the sole writer of <see cref="LifeStateComponent"/> after
/// <see cref="LifeStateInitializerSystem"/> attaches it. No other system may overwrite the
/// component. <see cref="CauseOfDeathComponent"/> is likewise attached only here.
///
/// Phase: <see cref="SystemPhase.Cleanup"/>. Must run AFTER any producer that may call
/// <see cref="RequestTransition"/> in the same tick (e.g. <see cref="ChokingDetectionSystem"/>,
/// SlipAndFallSystem, LockoutDetectionSystem) and BEFORE <see cref="ChokingCleanupSystem"/>.
///
/// Determinism: queued requests are processed in ascending NpcId order. Within a tick, multiple
/// requests targeting the same NPC dedupe by "later wins" (see <see cref="RequestTransition"/>).
/// IncapacitatedTickBudget countdown happens inline after queue drain; budget expiry enqueues
/// a Deceased transition that is drained by a single recursive <see cref="Update"/> call so the
/// transition completes in the same tick.
///
/// Legal transitions:
///  Alive → Incapacitated (with PendingDeathCause + IncapacitatedTickBudget)
///  Alive → Deceased (sudden death)
///  Incapacitated → Deceased (timeout or explicit request)
///  Deceased is terminal — further requests are ignored.
/// </remarks>
/// <seealso cref="LifeStateInitializerSystem"/>
/// <seealso cref="LifeStateGuard"/>
/// <seealso cref="LifeStateTransitionRequest"/>
public class LifeStateTransitionSystem : ISystem
{
    private readonly NarrativeEventBus    _narrativeBus;
    private readonly EntityManager        _em;
    private readonly SimulationClock      _clock;
    private readonly LifeStateConfig      _cfg;
    private readonly EntityRoomMembership _roomMembership;

    // Single-tick request queue. Cleared at end of each tick.
    private readonly List<LifeStateTransitionRequest> _queue = new();

    /// <summary>
    /// Constructs the life-state transition system.
    /// </summary>
    /// <param name="narrativeEventBus">Bus on which cause-of-death narrative candidates are raised.</param>
    /// <param name="entityManager">Entity manager held for future use; queries flow through the manager passed to <see cref="Update"/>.</param>
    /// <param name="clock">Simulation clock; supplies LastTransitionTick and DeathTick stamps.</param>
    /// <param name="config">Simulation config; <see cref="SimConfig.LifeState"/>.DefaultIncapacitatedTicks seeds the countdown for new Incapacitated transitions.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public LifeStateTransitionSystem(
        NarrativeEventBus    narrativeBus,
        EntityManager        em,
        SimulationClock      clock,
        LifeStateConfig      cfg,
        EntityRoomMembership roomMembership)
    {
        _narrativeBus   = narrativeBus;
        _em             = em;
        _clock          = clock;
        _cfg            = cfg;
        _roomMembership = roomMembership;
    }

    // ── Public API (called by scenario systems) ───────────────────────────────

    /// <summary>
    /// Enqueues a state transition request. The transition is applied at the end of
    /// the current tick (Cleanup phase). If the NPC has already received a request
    /// this tick, the later call wins (with a console warning).
    ///
    /// <para>Resurrection is forbidden: a request targeting
    /// <see cref="LifeState.Alive"/> on a <see cref="LifeState.Deceased"/> entity
    /// is silently dropped in <see cref="Update"/>.</para>
    /// </summary>
    /// <param name="npcId">Guid of the NPC to transition.</param>
    /// <param name="targetState">Desired new <see cref="Components.LifeState"/>.</param>
    /// <param name="cause">Cause of death recorded with the transition (use <see cref="CauseOfDeath.Unknown"/> when not applicable).</param>
    public void RequestTransition(Guid npcId, Components.LifeState targetState, CauseOfDeath cause)
    {
        // Deduplicate by npcId — later request wins.
        int existing = _queue.FindIndex(r => r.NpcId == npcId);
        if (existing >= 0)
        {
            Console.WriteLine(
                $"[LifeStateTransition] Warning: duplicate transition request for NPC {npcId} " +
                $"in the same tick. Previous ({_queue[existing].TargetState}/{_queue[existing].Cause}) " +
                $"replaced by ({target}/{cause}).");
            _queue.RemoveAt(existing);
        }
        _queue.Add(new LifeStateTransitionRequest(npcId, target, cause, incapacitationTicksOverride));
    }

    /// <summary>
    /// Drains the queued transition requests in ascending NpcId order, then ticks down
    /// IncapacitatedTickBudget for any Incapacitated NPC. When a budget expires, a Deceased
    /// transition is enqueued and processed via a single recursive call so it completes this tick.
    /// </summary>
    /// <param name="em">Entity manager used to look up NPCs and witnesses.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused; budget countdown is in ticks, not seconds).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        // 1. Process the tick's explicit transition requests (deterministic order).
        foreach (var req in _queue.OrderBy(r => r.NpcId))
            ApplyRequest(req);
        _queue.Clear();

        // 2. Tick down Incapacitated budgets; auto-transition to Deceased when expired.
        foreach (var entity in em.Query<NpcTag>().ToList())
        {
            if (!entity.Has<LifeStateComponent>()) continue;
            var ls = entity.Get<LifeStateComponent>();
            if (ls.State != LifeState.Incapacitated) continue;

            ls.IncapacitatedTickBudget--;
            if (ls.IncapacitatedTickBudget <= 0)
            {
                // Budget exhausted — die with the pending cause.
                var cause = ls.PendingDeathCause == CauseOfDeath.Unknown
                    ? CauseOfDeath.Unknown
                    : ls.PendingDeathCause;
                // Write the decremented budget before ApplyDeath overwrites the component.
                entity.Add(ls);
                ApplyDeath(entity, cause);
            }
            else
            {
                entity.Add(ls); // persist the decremented budget
            }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void ApplyRequest(LifeStateTransitionRequest req)
    {
        var entity = FindEntityByGuid(req.NpcId);
        if (entity is null) return;
        if (!entity.Has<LifeStateComponent>()) return;

        var current = entity.Get<LifeStateComponent>().State;

        // Resurrection is forbidden.
        if (req.TargetState == LifeState.Alive && current == LifeState.Deceased)
        {
            // Silent drop — no warning, by spec.
            return;
        }

        // Already dead — skip.
        if (current == LifeState.Deceased) return;

        switch (req.TargetState)
        {
            case LifeState.Incapacitated:
                ApplyIncapacitation(entity, req.Cause, req.IncapacitationTicksOverride);
                break;

            case LifeState.Deceased:
                ApplyDeath(entity, req.Cause);
                break;

            case LifeState.Alive:
                // Alive request from Incapacitated = rescue (valid but no rescue mechanic yet at v0.1).
                // Write the state change without emitting a death narrative.
                var lsRescue = entity.Get<LifeStateComponent>();
                lsRescue.State                  = LifeState.Alive;
                lsRescue.LastTransitionTick      = _clock.CurrentTick;
                lsRescue.IncapacitatedTickBudget = 0;
                lsRescue.PendingDeathCause       = CauseOfDeath.Unknown;
                entity.Add(lsRescue);
                break;
        }
    }

    private void ApplyIncapacitation(Entity entity, CauseOfDeath cause, int? tickBudgetOverride = null)
    {
        var budget = tickBudgetOverride ?? _cfg.DefaultIncapacitatedTicks;
        entity.Add(new LifeStateComponent
        {
            State                   = LifeState.Incapacitated,
            LastTransitionTick      = _clock.CurrentTick,
            IncapacitatedTickBudget = budget,
            PendingDeathCause       = cause,
        });
    }

    private void ApplyDeath(Entity entity, CauseOfDeath cause)
    {
        // 1. Find witness and room BEFORE flipping state (so the NPC is still Alive for proximity checks).
        var witnessId  = FindClosestWitness(entity);
        var locationId = GetRoomId(entity); // string? room uuid

        // 2. Emit the cause-of-death narrative candidate BEFORE flipping state so that
        //    MemoryRecordingSystem's synchronous bus handler sees Alive participants.
        var kind         = CauseToNarrativeKind(cause);
        var participants = witnessId == Guid.Empty
            ? new[] { EntityIntId(entity) }
            : new[] { EntityIntId(entity), EntityIntIdFromGuid(witnessId) };

        _narrativeBus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           _clock.CurrentTick,
            Kind:           kind,
            ParticipantIds: participants,
            RoomId:         locationId,
            Detail:         $"NPC {entity.Id} died of {cause}."
        ));

        // 3. Attach CauseOfDeathComponent.
        entity.Add(new CauseOfDeathComponent
        {
            Cause             = cause,
            DeathTick         = _clock.CurrentTick,
            WitnessedByNpcId  = witnessId,
            LocationRoomId    = locationId,
        });

        // 4. Flip LifeState to Deceased.
        entity.Add(new LifeStateComponent
        {
            State                   = LifeState.Deceased,
            LastTransitionTick      = _clock.CurrentTick,
            IncapacitatedTickBudget = 0,
            PendingDeathCause       = cause,
        });
    }

    // ── Witness selection ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the GUID of the smallest-EntityIntId Alive NPC within conversation
    /// range of the dying NPC. Returns <see cref="Guid.Empty"/> if none.
    /// Deterministic: consistent sort order ensures the same witness is selected
    /// across identical runs.
    /// </summary>
    /// <param name="deceased">The NPC who is about to be flagged Deceased.</param>
    /// <param name="em">Entity manager used to query NPCs.</param>
    /// <returns>The Guid of the chosen witness, or <see cref="Guid.Empty"/> if no eligible witness is in range.</returns>
    private Guid FindClosestWitness(Entity deceased, EntityManager em)
    {
        if (!dyingNpc.Has<PositionComponent>()) return Guid.Empty;
        var pos   = dyingNpc.Get<PositionComponent>();
        int range = dyingNpc.Has<ProximityComponent>()
            ? dyingNpc.Get<ProximityComponent>().ConversationRangeTiles
            : ProximityComponent.Default.ConversationRangeTiles;

        Guid   best     = Guid.Empty;
        int    bestId   = int.MaxValue;

        foreach (var candidate in _em.Query<NpcTag>().ToList())
        {
            if (candidate.Id == dyingNpc.Id) continue;
            if (!LifeStateGuard.IsAlive(candidate)) continue;
            if (!candidate.Has<PositionComponent>()) continue;

            var cPos = candidate.Get<PositionComponent>();
            float dx = cPos.X - pos.X;
            float dz = cPos.Z - pos.Z;
            float dist = MathF.Sqrt(dx * dx + dz * dz);

            if (dist <= range)
            {
                int intId = EntityIntId(candidate);
                if (intId < bestId)
                {
                    bestId = intId;
                    best   = candidate.Id;
                }
            }
        }

        return best;
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private Entity? FindEntityByGuid(Guid id)
    {
        foreach (var e in _em.GetAllEntities())
        {
            if (e.Id == id) return e;
        }
        return null;
    }

    private string? GetRoomId(Entity entity)
    {
        var room = _roomMembership.GetRoom(entity);
        if (room is null) return null;
        if (!room.Has<RoomComponent>()) return null;
        return room.Get<RoomComponent>().Id;
    }

    /// <summary>Extract the integer ID from entity's Guid (stored in bytes 0-7 little-endian).</summary>
    /// <param name="entity">The entity to extract the integer ID from.</param>
    /// <returns>The first 4 bytes of the entity's Guid interpreted as a little-endian Int32.</returns>
    private static int GetEntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    /// <summary>Map CauseOfDeath to the corresponding NarrativeEventKind.</summary>
    /// <param name="cause">The cause to translate.</param>
    /// <returns>A specific narrative kind (Choked, SlippedAndFell, StarvedAlone) or the generic <see cref="Narrative.NarrativeEventKind.Died"/> fallback.</returns>
    private static Narrative.NarrativeEventKind CauseToNarrativeKind(CauseOfDeath cause) => cause switch
    {
        var b = id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    private static NarrativeEventKind CauseToNarrativeKind(CauseOfDeath cause) => cause switch
    {
        CauseOfDeath.Choked         => NarrativeEventKind.Choked,
        CauseOfDeath.SlippedAndFell => NarrativeEventKind.SlippedAndFell,
        CauseOfDeath.StarvedAlone   => NarrativeEventKind.StarvedAlone,
        _                           => NarrativeEventKind.Died,
    };

    // ── Test seam ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the pending request for the given NPC if one exists this tick.
    /// For test use only — do not call from production code.
    /// </summary>
    internal LifeStateTransitionRequest? PeekQueue(Guid npcId) =>
        _queue.FirstOrDefault(r => r.NpcId == npcId);
}
