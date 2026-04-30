using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems.Spatial;

/// <summary>
/// Phase: Spatial (5) — first system to run in the spatial phase.
///
/// Keeps the ISpatialIndex synchronized with the live PositionComponent state:
///   - Registers entities that have just received a PositionComponent.
///   - Updates the index when an entity's tile position changes.
///   - Unregisters destroyed entities (via the EntityManager.EntityDestroyed event).
///   - Emits StructuralChangeEvent on StructuralChangeBus when a StructuralTag entity moves.
///
/// Tile coordinates: (int)(Math.Round(pos.X)), (int)(Math.Round(pos.Z)).
/// </summary>
public sealed class SpatialIndexSyncSystem : ISystem
{
    private readonly ISpatialIndex _index;
    private readonly StructuralChangeBus _structuralBus;

    // entity → last-synced tile position; presence in this dict means "registered"
    private readonly Dictionary<Entity, (int x, int y)> _lastPos = new();
    private long _tickCounter = 0;

    public SpatialIndexSyncSystem(ISpatialIndex index, StructuralChangeBus structuralBus)
    {
        _index = index;
        _structuralBus = structuralBus;
    }

    /// <summary>
    /// Called by SimulationBootstrapper when EntityManager.EntityDestroyed fires.
    /// Removes the entity from the spatial index immediately.
    /// If the entity has StructuralTag, emits EntityRemoved on StructuralChangeBus.
    /// </summary>
    public void OnEntityDestroyed(Entity entity)
    {
        if (_lastPos.ContainsKey(entity))
        {
            // Get position before unregistering
            var pos = _lastPos[entity];

            _index.Unregister(entity);
            _lastPos.Remove(entity);

            // If it has StructuralTag, emit EntityRemoved
            if (entity.Has<StructuralTag>())
            {
                _structuralBus.Emit(
                    StructuralChangeKind.EntityRemoved,
                    entity.Id,
                    pos.x, pos.y,
                    pos.x, pos.y,
                    Guid.Empty,
                    _tickCounter++
                );
            }
        }
    }

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<PositionComponent>())
        {
            var pos = entity.Get<PositionComponent>();
            int tx = (int)Math.Round(pos.X);
            int ty = (int)Math.Round(pos.Z);

            if (!_lastPos.TryGetValue(entity, out var last))
            {
                // Newly spawned — register
                _index.Register(entity, tx, ty);
                _lastPos[entity] = (tx, ty);

                // If it has StructuralTag, emit EntityAdded
                if (entity.Has<StructuralTag>())
                {
                    _structuralBus.Emit(
                        StructuralChangeKind.EntityAdded,
                        entity.Id,
                        tx, ty,
                        tx, ty,
                        Guid.Empty,
                        _tickCounter++
                    );
                }
            }
            else if (last.x != tx || last.y != ty)
            {
                // Position changed — update cell
                _index.Update(entity, tx, ty);
                _lastPos[entity] = (tx, ty);

                // If it has StructuralTag, emit EntityMoved
                if (entity.Has<StructuralTag>())
                {
                    _structuralBus.Emit(
                        StructuralChangeKind.EntityMoved,
                        entity.Id,
                        last.x, last.y,
                        tx, ty,
                        Guid.Empty,
                        _tickCounter++
                    );
                }
            }
        }
    }
}
