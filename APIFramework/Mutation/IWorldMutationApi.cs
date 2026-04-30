using APIFramework.Components;

namespace APIFramework.Mutation;

/// <summary>
/// Public contract for mutating the world topology at runtime.
/// All structural mutations after WP-3.0.4 flow through this interface.
///
/// Each method validates inputs, applies the mutation, and emits the corresponding
/// StructuralChangeEvent so subscribers (including the pathfinding cache invalidator) can react.
///
/// Tests, 3.0.3's stain spawner, and 3.1.D's Unity glue all use this API.
/// Direct entity mutations outside this API are code smell and should be escalated.
/// </summary>
public interface IWorldMutationApi
{
    /// <summary>
    /// Move a MutableTopologyTag entity to a new tile position.
    /// Fails closed (returns false) if the entity lacks MutableTopologyTag or if the move is invalid.
    /// On success, emits EntityMoved on StructuralChangeBus.
    /// </summary>
    bool MoveEntity(Guid entityId, int newTileX, int newTileY);

    /// <summary>
    /// Spawn a structural entity at a given tile (e.g., a desk placed by the player in 3.1.D).
    /// The entity is created from a template and must have StructuralTag.
    /// Emits EntityAdded on StructuralChangeBus.
    /// Returns the created entity's Guid, or Guid.Empty if spawn failed.
    /// </summary>
    Guid SpawnStructural(Guid templateId, int tileX, int tileY);

    /// <summary>
    /// Despawn a structural entity (e.g., a desk removed by the player).
    /// Emits EntityRemoved on StructuralChangeBus.
    /// Fails closed (does nothing) if the entity is not found or lacks StructuralTag.
    /// </summary>
    bool DespawnStructural(Guid entityId);

    /// <summary>
    /// Attach ObstacleTag to an entity (e.g., marking a tile as a fall risk).
    /// Emits ObstacleAttached on StructuralChangeBus.
    /// Fails closed if the entity is not found.
    /// </summary>
    bool AttachObstacle(Guid entityId);

    /// <summary>
    /// Remove ObstacleTag from an entity.
    /// Emits ObstacleDetached on StructuralChangeBus.
    /// Fails closed if the entity is not found or lacks ObstacleTag.
    /// </summary>
    bool DetachObstacle(Guid entityId);

    /// <summary>
    /// Change a room's bounding box (e.g., when 3.1.D adds a wall to a room).
    /// Emits RoomBoundsChanged on StructuralChangeBus.
    /// Fails closed if the room entity is not found or lacks RoomComponent.
    /// </summary>
    bool ChangeRoomBounds(Guid roomId, BoundsRect newBounds);
}
