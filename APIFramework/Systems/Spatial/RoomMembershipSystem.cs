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
///
/// When a StructuralChangeBus is provided:
///   - Emits RoomBoundsChanged when a room entity's Bounds change.
///   - NPC room transitions do NOT emit on the structural bus — they are proximity-level,
///     not topology-level. This is a critical guardrail enforced here.
/// </summary>
public sealed class RoomMembershipSystem : ISystem
{
    private readonly EntityRoomMembership _membership;
    private readonly ProximityEventBus    _proximityBus;
    private readonly StructuralChangeBus? _structuralBus;

    // room entity → last-seen Bounds; used to detect room bounds changes
    private readonly Dictionary<Entity, BoundsRect> _lastRoomBounds = new();

    private int _tick;

    public RoomMembershipSystem(EntityRoomMembership membership, ProximityEventBus proximityBus,
        StructuralChangeBus? structuralBus = null)
    {
        _membership    = membership;
        _proximityBus  = proximityBus;
        _structuralBus = structuralBus;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        _tick++;

        // Check for room bounds changes before membership resolution
        if (_structuralBus != null)
        {
            foreach (var roomEntity in em.Query<RoomTag>())
            {
                if (!roomEntity.Has<RoomComponent>()) continue;
                var current = roomEntity.Get<RoomComponent>().Bounds;

                if (_lastRoomBounds.TryGetValue(roomEntity, out var prev))
                {
                    if (prev != current)
                    {
                        _lastRoomBounds[roomEntity] = current;
                        _structuralBus.Emit(StructuralChangeKind.RoomBoundsChanged, roomEntity.Id,
                            prev.X, prev.Y, current.X, current.Y, roomEntity.Id, _tick);
                    }
                }
                else
                {
                    _lastRoomBounds[roomEntity] = current;
                }
            }
        }

        // Snapshot rooms once per tick; sorted by area ascending so the first match wins
        var rooms = em.Query<RoomTag>()
            .Where(re => re.Has<RoomComponent>())
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
                // NPC room transitions fire on ProximityEventBus only — never on structural bus
                _proximityBus.RaiseRoomMembershipChanged(new RoomMembershipChanged(entity, oldRoom, newRoom, _tick));
            }
        }
    }
}
