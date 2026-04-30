using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems.Spatial;

/// <summary>
/// Phase: Spatial (5) — first system to run in the spatial phase.
///
/// Keeps the ISpatialIndex synchronized with the live PositionComponent state:
///   - Registers entities that have just received a PositionComponent.
///   - Updates the index when an entity's tile position changes.
///   - Unregisters destroyed entities (via the EntityManager.EntityDestroyed event).
///
/// Tile coordinates: (int)(Math.Round(pos.X)), (int)(Math.Round(pos.Z)).
/// </summary>
/// <remarks>
/// Reads <c>PositionComponent</c>; writes to the <see cref="ISpatialIndex"/>. Single-writer
/// rule for the index: only this system registers / updates / unregisters entries.
/// Skips Deceased NPCs — their last-tick position remains in the index.
/// </remarks>
public sealed class SpatialIndexSyncSystem : ISystem
{
    private readonly ISpatialIndex _index;

    // entity → last-synced tile position; presence in this dict means "registered"
    private readonly Dictionary<Entity, (int x, int y)> _lastPos = new();

    /// <summary>
    /// Stores the spatial-index reference.
    /// </summary>
    /// <param name="index">Cell-based spatial index that this system synchronizes.</param>
    public SpatialIndexSyncSystem(ISpatialIndex index)
    {
        _index = index;
    }

    /// <summary>
    /// Called by SimulationBootstrapper when EntityManager.EntityDestroyed fires.
    /// Removes the entity from the spatial index immediately.
    /// </summary>
    /// <param name="entity">The entity that was just destroyed.</param>
    public void OnEntityDestroyed(Entity entity)
    {
        if (_lastPos.ContainsKey(entity))
        {
            _index.Unregister(entity);
            _lastPos.Remove(entity);
        }
    }

    /// <summary>
    /// Per-tick entry point. Registers newly-spawned entities and updates moved ones.
    /// </summary>
    /// <param name="em">Entity manager — queried for positioned entities.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<PositionComponent>())
        {
            if (!LifeStateGuard.IsBiologicallyTicking(entity)) continue;  // WP-3.0.0: Deceased position is frozen; prior tick's index entry remains valid

            var pos = entity.Get<PositionComponent>();
            int tx = (int)Math.Round(pos.X);
            int ty = (int)Math.Round(pos.Z);

            if (!_lastPos.TryGetValue(entity, out var last))
            {
                // Newly spawned — register
                _index.Register(entity, tx, ty);
                _lastPos[entity] = (tx, ty);
            }
            else if (last.x != tx || last.y != ty)
            {
                // Position changed — update cell
                _index.Update(entity, tx, ty);
                _lastPos[entity] = (tx, ty);
            }
        }
    }
}
