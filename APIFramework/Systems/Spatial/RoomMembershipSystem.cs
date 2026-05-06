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
/// <remarks>
/// Reads: <see cref="RoomTag"/>+<see cref="RoomComponent"/> (room geometry) and
/// <see cref="PositionComponent"/> on every other entity.
/// Writes: the <see cref="EntityRoomMembership"/> map (single writer); emits
/// <see cref="ProximityEventBus.RaiseRoomMembershipChanged"/> on transitions and
/// <see cref="StructuralChangeKind.RoomBoundsChanged"/> on geometry changes.
/// Ordering: must run after <see cref="SpatialIndexSyncSystem"/> so positions are current,
/// and before <see cref="ProximityEventSystem"/> so it sees the current membership map.
/// </remarks>
/// <seealso cref="EntityRoomMembership"/>
/// <seealso cref="ProximityEventBus"/>
/// <seealso cref="StructuralChangeBus"/>
public sealed class RoomMembershipSystem : ISystem
{
    private readonly EntityRoomMembership _membership;
    private readonly ProximityEventBus    _bus;
    private readonly StructuralChangeBus  _structuralBus;
    private readonly Dictionary<Entity, BoundsRect> _lastRoomBounds = new();
    private int _tick;

    /// <summary>
    /// Constructs the system.
    /// </summary>
    /// <param name="membership">Runtime entity-to-room map this system maintains.</param>
    /// <param name="bus">Proximity event bus that receives <c>RoomMembershipChanged</c> events.</param>
    /// <param name="structuralBus">Structural change bus that receives <c>RoomBoundsChanged</c> events.</param>
    public RoomMembershipSystem(EntityRoomMembership membership, ProximityEventBus bus, StructuralChangeBus structuralBus)
    {
        _membership = membership;
        _bus        = bus;
        _structuralBus = structuralBus;
    }

    /// <summary>
    /// Per-tick update. Detects room bounds changes (emitting structural events), then
    /// resolves containing-room membership for every positioned non-room entity and
    /// emits proximity events on transitions.
    /// </summary>
    public void Update(EntityManager em, float deltaTime)
    {
        _tick++;

        // Check for room bounds changes before membership resolution
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

        // Snapshot rooms once per tick; sorted by area ascending so the first match wins
        var rooms = em.Query<RoomTag>()
            .Where(re => re.Has<RoomComponent>())
            .Select(re => (entity: re, room: re.Get<RoomComponent>()))
            .OrderBy(r => r.room.Bounds.Area)
            .ThenBy(r => r.entity.Id)
            .ToList();

        // Process positioned non-room entities in id order for determinism
        foreach (var entity in em.Query<PositionComponent>().OrderBy(e => e.Id))
        {
            if (entity.Has<RoomTag>()) continue;

            var pos = entity.Get<PositionComponent>();
            int tx = (int)Math.Round(pos.X);
            int ty = (int)Math.Round(pos.Z);

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
                _bus.RaiseRoomMembershipChanged(new RoomMembershipChanged(entity, oldRoom, newRoom, _tick));
            }
        }
    }
}
