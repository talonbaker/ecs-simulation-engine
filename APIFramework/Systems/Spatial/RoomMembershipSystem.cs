using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems.Spatial;

/// <summary>
/// Phase: Spatial (5) — runs after SpatialIndexSyncSystem, before ProximityEventSystem.
///
/// Each tick, for every positioned non-room entity:
///   - Computes which room entity contains the entity's tile position (point-in-rect).
///   - When multiple rooms overlap, picks the one with the smallest area (most-specific wins).
///   - Caches the result in EntityRoomMembership.
///   - Fires RoomMembershipChanged on ProximityEventBus when the result changes.
///   - Detects room bounds changes and emits RoomBoundsChanged on StructuralChangeBus.
/// </summary>
public sealed class RoomMembershipSystem : ISystem
{
    private readonly EntityRoomMembership _membership;
    private readonly ProximityEventBus    _bus;
    private readonly StructuralChangeBus  _structuralBus;
    private readonly Dictionary<Entity, BoundsRect> _lastRoomBounds = new();
    private int _tick;

    public RoomMembershipSystem(EntityRoomMembership membership, ProximityEventBus bus, StructuralChangeBus structuralBus)
    {
        _membership = membership;
        _bus        = bus;
        _structuralBus = structuralBus;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        _tick++;

        // Check for room bounds changes and emit events
        var roomEntities = em.Query<RoomTag>()
            .Where(re => re.Has<RoomComponent>())
            .ToList();

        foreach (var roomEntity in roomEntities)
        {
            var room = roomEntity.Get<RoomComponent>();

            if (_lastRoomBounds.TryGetValue(roomEntity, out var lastBounds))
            {
                if (!lastBounds.Equals(room.Bounds))
                {
                    // Bounds changed — emit event
                    _structuralBus.Emit(
                        StructuralChangeKind.RoomBoundsChanged,
                        roomEntity.Id,
                        0, 0,
                        0, 0,
                        roomEntity.Id,
                        _tick
                    );
                    _lastRoomBounds[roomEntity] = room.Bounds;
                }
            }
            else
            {
                // First time seeing this room — just cache the bounds
                _lastRoomBounds[roomEntity] = room.Bounds;
            }
        }

        // Snapshot rooms once per tick; sorted by area ascending so the first match wins
        var rooms = roomEntities
            .Select(re => (entity: re, room: re.Get<RoomComponent>()))
            .OrderBy(r => r.room.Bounds.Area)
            .ThenBy(r => r.entity.Id)   // deterministic tiebreak on equal area
            .ToList();

        // Process positioned non-room entities in id order for determinism
        foreach (var entity in em.Query<PositionComponent>().OrderBy(e => e.Id))
        {
            if (entity.Has<RoomTag>()) continue;

            var pos = entity.Get<PositionComponent>();
            int tx = (int)Math.Round(pos.X);
            int ty = (int)Math.Round(pos.Z);

            // First containing room in the sorted list wins (smallest area)
            Entity? newRoom = null;
            foreach (var (re, rc) in rooms)
            {
                if (rc.Bounds.Contains(tx, ty))
                {
                    newRoom = re;
                    break;
                }
            }

            var oldRoom = _membership.GetRoom(entity);
            _membership.SetRoom(entity, newRoom);

            if (!ReferenceEquals(oldRoom, newRoom))
            {
                _bus.RaiseRoomMembershipChanged(new RoomMembershipChanged(entity, oldRoom, newRoom, _tick));
            }
        }
    }
}
