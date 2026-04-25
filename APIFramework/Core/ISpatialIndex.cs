namespace APIFramework.Core;

/// <summary>
/// Contract for spatial lookup structures used by v0.9+ world systems.
///
/// WHY AN INTERFACE NOW
/// ─────────────────────
/// v0.8 introduces a 2-D world grid. Systems that need to know "which entities
/// are near position X?" should depend on ISpatialIndex, not on any concrete
/// spatial data structure. This lets us swap implementations freely:
///
///   - Simple grid (v0.9 default, O(1) per cell lookup)
///   - Quadtree (dense worlds, many small entities)
///   - BVH      (large entities with varying sizes)
///   - No-op stub for headless tests
///
/// Systems that receive ISpatialIndex via constructor injection are automatically
/// decoupled from the choice of data structure. SimulationBootstrapper decides
/// which implementation to hand out.
///
/// CURRENT STATUS
/// ───────────────
/// This interface is a stub — no production implementation exists yet.
/// It is defined now so that:
///   1. v0.8 world component design can reference it from the start.
///   2. Systems can declare their spatial dependency in their constructors.
///   3. The contract can be reviewed and refined before any concrete code exists.
///
/// COORDINATE SYSTEM (proposed)
/// ─────────────────────────────
/// Positions are (float x, float y) in world-space metres.
/// The exact type (ValueTuple vs a Vector2 record) is TBD in v0.8.
/// </summary>
public interface ISpatialIndex
{
    /// <summary>
    /// Registers an entity at the given world-space position.
    /// Called once when an entity spawns or receives a PositionComponent.
    /// </summary>
    void Register(Entity entity, float x, float y);

    /// <summary>
    /// Removes an entity from the index.
    /// Called when an entity is destroyed or loses its PositionComponent.
    /// </summary>
    void Unregister(Entity entity);

    /// <summary>
    /// Updates the stored position of an entity that has moved.
    /// Called by movement/transit systems after updating PositionComponent.
    /// </summary>
    void Update(Entity entity, float newX, float newY);

    /// <summary>
    /// Returns all entities within <paramref name="radius"/> metres of the given point.
    /// Result set is unordered; callers should not assume any specific order.
    /// </summary>
    IEnumerable<Entity> QueryRadius(float x, float y, float radius);

    /// <summary>
    /// Returns the <paramref name="maxCount"/> nearest entities to the given point,
    /// sorted ascending by distance. Returns fewer results if fewer exist.
    /// </summary>
    IEnumerable<Entity> QueryNearest(float x, float y, int maxCount);
}
