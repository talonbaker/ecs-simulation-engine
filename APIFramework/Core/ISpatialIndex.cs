namespace APIFramework.Core;

/// <summary>
/// Contract for spatial lookup structures used by the building's spatial layer.
///
/// WHY AN INTERFACE
/// ────────────────
/// Systems that need to know "which entities are near position X?" depend on
/// ISpatialIndex, not on any concrete spatial data structure. This lets the
/// implementation be swapped freely:
///
///   - GridSpatialIndex  (default — cell-based grid, WP-1.1.A)
///   - Quadtree          (dense worlds with many small entities)
///   - BVH               (large entities with varying sizes)
///   - No-op stub        (headless tests)
///
/// COORDINATE SYSTEM
/// ──────────────────
/// Positions are (int x, int y) in tile units. The horizontal plane maps to
/// PositionComponent.X (tileX) and PositionComponent.Z (tileY). Tile integers
/// match the v0.3 schema's BoundsRect integers and the grid-aligned building.
/// Sub-tile precision is unnecessary at office-simulation fidelity.
///
/// QUERY RESULT CONTRACT
/// ──────────────────────
/// Results are materialized eagerly as IReadOnlyList&lt;Entity&gt; so callers can
/// enumerate multiple times without recomputing. Order is deterministic:
/// cells visited in row-major order; entities within a cell in insertion order.
/// QueryNearest returns the n nearest sorted ascending by distance, ties broken
/// by Entity.Id ascending.
/// </summary>
public interface ISpatialIndex
{
    /// <summary>
    /// Registers an entity at the given tile position.
    /// Call once when an entity spawns or receives a PositionComponent.
    /// </summary>
    void Register(Entity entity, int x, int y);

    /// <summary>
    /// Removes an entity from the index.
    /// Call when an entity is destroyed or loses its PositionComponent.
    /// </summary>
    void Unregister(Entity entity);

    /// <summary>
    /// Updates the stored tile position of an entity that has moved.
    /// No-op if the entity maps to the same cell as before.
    /// </summary>
    void Update(Entity entity, int newX, int newY);

    /// <summary>
    /// Returns all entities whose tile position is within <paramref name="radius"/> tiles of
    /// <c>(x, y)</c>. Uses exact integer distance (squared comparison avoids sqrt).
    /// Result is row-major ordered and deterministic.
    /// </summary>
    IReadOnlyList<Entity> QueryRadius(int x, int y, int radius);

    /// <summary>
    /// Returns the <paramref name="maxCount"/> nearest entities to <c>(x, y)</c>,
    /// sorted ascending by distance. Ties broken by Entity.Id ascending.
    /// Returns fewer results if fewer are registered.
    /// </summary>
    IReadOnlyList<Entity> QueryNearest(int x, int y, int maxCount);
}
