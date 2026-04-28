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
public sealed class SpatialIndexSyncSystem : ISystem
{
    private readonly ISpatialIndex _index;

    // entity → last-synced tile position; presence in this dict means "registered"
    private readonly Dictionary<Entity, (int x, int y)> _lastPos = new();

    public SpatialIndexSyncSystem(ISpatialIndex index)
    {
        _index = index;
    }

    /// <summary>
    /// Called by SimulationBootstrapper when EntityManager.EntityDestroyed fires.
    /// Removes the entity from the spatial index immediately.
    /// </summary>
    public void OnEntityDestroyed(Entity entity)
    {
        if (_lastPos.ContainsKey(entity))
        {
            _index.Unregister(entity);
            _lastPos.Remove(entity);
        }
    }

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
