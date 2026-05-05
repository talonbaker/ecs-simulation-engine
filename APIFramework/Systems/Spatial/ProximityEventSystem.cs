using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems.Spatial;

/// <summary>
/// Phase: Spatial (5) — runs after RoomMembershipSystem.
///
/// For each NPC with a ProximityComponent, queries the spatial index for nearby NPCs
/// and fires discrete proximity events when neighbors enter or leave each range bucket:
///
///   ConversationRange  — within ProximityComponent.ConversationRangeTiles
///   RoomPresence       — in the same room (EntityRoomMembership), not in conversation range
///   Visible            — within AwarenessRangeTiles, not conversation or same-room
///
/// Events are batched during the tick and fired at end-of-tick in entity-id-ascending order.
/// This gives consumers a consistent snapshot rather than a mid-tick partial view.
/// </summary>
/// <remarks>
/// Reads <c>PositionComponent</c>, <c>ProximityComponent</c>, and the <see cref="EntityRoomMembership"/>
/// snapshot. Writes nothing to the entity world — only publishes events on
/// <see cref="ProximityEventBus"/>. Skips non-Alive observers.
/// Note: registered in <c>SystemPhase.Lighting</c> (not Spatial)
/// in <see cref="APIFramework.Core.SimulationBootstrapper"/> so it fires after illumination
/// is current and visibility checks are correct.
/// </remarks>
public sealed class ProximityEventSystem : ISystem
{
    private readonly ISpatialIndex        _index;
    private readonly ProximityEventBus    _bus;
    private readonly EntityRoomMembership _membership;

    // Previous-tick neighbor sets per NPC per range bucket
    private readonly Dictionary<Entity, List<Entity>> _prevConversation = new();
    private readonly Dictionary<Entity, List<Entity>> _prevRoom         = new();
    private readonly Dictionary<Entity, List<Entity>> _prevVisible      = new();

    // Pooled batch — cleared each tick, filled, sorted, then fired
    private readonly List<PendingEvent> _pending = new();

    private int _tick;

    private enum EventKind
    {
        EnteredConversation, LeftConversation,
        EnteredRoom,         LeftRoom,
        Visible,             // entry only; no LeftVisible in the spec
    }

    private readonly record struct PendingEvent(Entity Observer, Entity Target, EventKind Kind);

    /// <summary>
    /// Stores spatial-index, bus, and membership references used per tick.
    /// </summary>
    /// <param name="index">Cell-based spatial index — used for radius queries.</param>
    /// <param name="bus">Bus on which proximity events are published.</param>
    /// <param name="membership">Room-membership lookup used to bucket targets into ROOM vs VISIBLE.</param>
    public ProximityEventSystem(ISpatialIndex index, ProximityEventBus bus, EntityRoomMembership membership)
    {
        _index      = index;
        _bus        = bus;
        _membership = membership;
    }

    /// <summary>
    /// Per-tick entry point. Recomputes per-NPC neighbor buckets, diffs them against the
    /// previous tick, and publishes the resulting batch of events in entity-id-ascending order.
    /// </summary>
    /// <param name="em">Entity manager — queried for proximity-capable entities.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        _tick++;
        _pending.Clear();

        // Process NPCs in id-ascending order for determinism
        var npcs = em.Query<ProximityComponent>().OrderBy(e => e.Id).ToList();

        foreach (var npcA in npcs)
        {
            if (!LifeStateGuard.IsAlive(npcA)) continue;  // WP-3.0.0: deceased NPCs do not act as proximity observers
            if (!npcA.Has<PositionComponent>()) continue;

            var posA  = npcA.Get<PositionComponent>();
            int tx    = (int)Math.Round(posA.X);
            int ty    = (int)Math.Round(posA.Z);
            var prox  = npcA.Get<ProximityComponent>();
            var roomA = _membership.GetRoom(npcA);

            // Query awareness range — covers conversation (subset) and awareness
            var nearby = _index.QueryRadius(tx, ty, prox.AwarenessRangeTiles);

            var convNow = new List<Entity>();
            var roomNow = new List<Entity>();
            var visNow  = new List<Entity>();

            // Iterate in id order for deterministic bucket assignment
            foreach (var entityB in nearby.OrderBy(e => e.Id))
            {
                if (ReferenceEquals(entityB, npcA)) continue;
                if (!entityB.Has<ProximityComponent>()) continue;

                var posB = entityB.Get<PositionComponent>();
                int bx   = (int)Math.Round(posB.X);
                int by   = (int)Math.Round(posB.Z);
                int dx   = bx - tx, dy = by - ty;
                double dist = Math.Sqrt((double)dx * dx + (double)dy * dy);

                if (dist <= prox.ConversationRangeTiles)
                {
                    convNow.Add(entityB);
                }
                else
                {
                    var roomB = _membership.GetRoom(entityB);
                    if (roomA != null && roomB != null && ReferenceEquals(roomA, roomB))
                        roomNow.Add(entityB);
                    else
                        visNow.Add(entityB);
                }
            }

            // Diff each bucket against previous tick
            DiffBucket(npcA, _prevConversation, convNow, EventKind.EnteredConversation, EventKind.LeftConversation);
            DiffBucket(npcA, _prevRoom,         roomNow, EventKind.EnteredRoom,         EventKind.LeftRoom);
            DiffBucket(npcA, _prevVisible,      visNow,  EventKind.Visible,             leaveKind: null);

            // Update previous state
            _prevConversation[npcA] = convNow;
            _prevRoom[npcA]         = roomNow;
            _prevVisible[npcA]      = visNow;
        }

        // Sort by (Observer.Id, Target.Id) then fire — entity-id-ascending order per spec
        _pending.Sort((a, b) =>
        {
            int cmp = a.Observer.Id.CompareTo(b.Observer.Id);
            return cmp != 0 ? cmp : a.Target.Id.CompareTo(b.Target.Id);
        });

        foreach (var ev in _pending)
        {
            switch (ev.Kind)
            {
                case EventKind.EnteredConversation:
                    _bus.RaiseEnteredConversationRange(new ProximityEnteredConversationRange(ev.Observer, ev.Target, _tick));
                    break;
                case EventKind.LeftConversation:
                    _bus.RaiseLeftConversationRange(new ProximityLeftConversationRange(ev.Observer, ev.Target, _tick));
                    break;
                case EventKind.EnteredRoom:
                    _bus.RaiseEnteredRoom(new ProximityEnteredRoom(ev.Observer, ev.Target, _tick));
                    break;
                case EventKind.LeftRoom:
                    _bus.RaiseLeftRoom(new ProximityLeftRoom(ev.Observer, ev.Target, _tick));
                    break;
                case EventKind.Visible:
                    _bus.RaiseVisibleFromHere(new ProximityVisibleFromHere(ev.Observer, ev.Target, _tick));
                    break;
            }
        }
    }

    private void DiffBucket(
        Entity observer,
        Dictionary<Entity, List<Entity>> prevMap,
        List<Entity> current,
        EventKind enterKind,
        EventKind? leaveKind)
    {
        if (!prevMap.TryGetValue(observer, out var prev))
            prev = new List<Entity>();

        foreach (var e in current)
        {
            if (!prev.Contains(e))
                _pending.Add(new PendingEvent(observer, e, enterKind));
        }

        if (leaveKind.HasValue)
        {
            foreach (var e in prev)
            {
                if (!current.Contains(e))
                    _pending.Add(new PendingEvent(observer, e, leaveKind.Value));
            }
        }
    }
}
