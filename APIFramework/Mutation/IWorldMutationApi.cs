using System;
using APIFramework.Components;

namespace APIFramework.Mutation;

/// <summary>
/// Public contract for all runtime structural topology mutations.
/// All structural mutations after WP-3.0.4 flow through this interface.
/// Tests, WP-3.0.3 stain spawner, and WP-3.1.D Unity glue all call through here.
///
/// Direct entity.Set(new PositionComponent(...)) on a StructuralTag entity
/// outside this API is a code smell — it bypasses bus emission and leaves the
/// pathfinding cache stale.
///
/// Boot-time spawns (WorldDefinitionLoader, SpawnWorld) are exempt: they happen
/// before the bus has subscribers and do not need cache invalidation.
/// </summary>
public interface IWorldMutationApi
{
    /// <summary>
    /// Moves a MutableTopologyTag entity to a new tile.
    /// Throws InvalidOperationException if the entity lacks MutableTopologyTag (fail-closed).
    /// </summary>
    void MoveEntity(Guid entityId, int newTileX, int newTileY);

    /// <summary>Spawns a new structural entity at the given tile. Returns the new entity's ID.</summary>
    Guid SpawnStructural(int tileX, int tileY);

    /// <summary>Despawns a StructuralTag entity and emits EntityRemoved.</summary>
    void DespawnStructural(Guid entityId);

    /// <summary>Attaches ObstacleTag and StructuralTag to an existing entity.</summary>
    void AttachObstacle(Guid entityId);

    /// <summary>Removes ObstacleTag from an entity. StructuralTag is retained.</summary>
    void DetachObstacle(Guid entityId);

    /// <summary>Updates RoomComponent.Bounds for the given room entity.</summary>
    void ChangeRoomBounds(Guid roomId, BoundsRect newBounds);
}
