using System.Collections.Generic;
using APIFramework.Core;

namespace APIFramework.Systems.Spatial;

/// <summary>
/// Service that tracks which room entity each positioned entity currently occupies.
/// Updated each tick by RoomMembershipSystem; queried by ProximityEventSystem and
/// future social/behavior systems.
///
/// Null room means the entity is outside all room bounds (e.g. in the parking lot
/// geometry that has no explicit room entity yet, or an entity with no position).
/// </summary>
public sealed class EntityRoomMembership
{
    private readonly Dictionary<Entity, Entity?> _map = new();

    /// <summary>Returns the room entity the given entity currently occupies, or null.</summary>
    public Entity? GetRoom(Entity entity) =>
        _map.TryGetValue(entity, out var room) ? room : null;

    /// <summary>Records that <paramref name="entity"/> is now in <paramref name="room"/> (may be null).</summary>
    public void SetRoom(Entity entity, Entity? room) => _map[entity] = room;

    /// <summary>Removes the membership record for a destroyed entity.</summary>
    public void Remove(Entity entity) => _map.Remove(entity);

    /// <summary>All entities with a membership record. Used for iteration by tests.</summary>
    public IEnumerable<Entity> Entities => _map.Keys;
}
