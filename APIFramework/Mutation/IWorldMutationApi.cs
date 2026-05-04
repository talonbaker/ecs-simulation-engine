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

    /// <summary>
    /// Attaches ThrownVelocityComponent and ThrownTag to an existing entity, launching it.
    /// </summary>
    void ThrowEntity(Guid entityId, float velocityX, float velocityZ, float velocityY, float decayPerTick);

    /// <summary>
    /// Spawns a stain entity from a template (see StainTemplates) at the given tile.
    /// Attaches StainTag, StainComponent, FallRiskComponent, and PositionComponent.
    /// Returns the new entity's ID.
    /// </summary>
    Guid SpawnStain(string templateId, int tileX, int tileY);

    // ── Author-mode extensions (WP-4.0.J) ────────────────────────────────────────

    /// <summary>
    /// Spawns a new room entity with the given category, floor, and bounds.
    /// Returns the new room entity's ID. Emits StructuralChangeKind.EntityAdded.
    /// </summary>
    Guid CreateRoom(RoomCategory category, BuildingFloor floor, BoundsRect bounds, string? name = null);

    /// <summary>
    /// Despawns a room. <paramref name="policy"/> controls whether contents are orphaned
    /// or cascade-deleted (NPC slots are never cascade-deleted regardless of policy).
    /// Emits StructuralChangeKind.EntityRemoved for each despawned entity.
    /// </summary>
    void DespawnRoom(Guid roomId, RoomDespawnPolicy policy);

    /// <summary>
    /// Spawns a light source in the named room at the given tile.
    /// Returns the new light entity's ID.
    /// </summary>
    Guid CreateLightSource(string roomId, int tileX, int tileY,
                           LightKind kind, LightState state, int intensity, int colorTempK);

    /// <summary>
    /// Mutates a light source's tunable properties in place (state, intensity, color temperature).
    /// Throws InvalidOperationException if the entity is not a light source.
    /// </summary>
    void TuneLightSource(Guid lightId, LightState state, int intensity, int colorTempK);

    /// <summary>
    /// Spawns a light aperture (window/skylight) on the boundary of a room.
    /// Returns the new aperture entity's ID.
    /// </summary>
    Guid CreateLightAperture(string roomId, int tileX, int tileY,
                             ApertureFacing facing, double areaSqTiles);

    /// <summary>
    /// Despawns a light source or aperture entity.
    /// Throws InvalidOperationException if the entity is neither.
    /// </summary>
    void DespawnLight(Guid lightId);
}
