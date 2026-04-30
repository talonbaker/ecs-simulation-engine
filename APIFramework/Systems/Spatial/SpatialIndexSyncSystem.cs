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
///
/// When a StructuralChangeBus is provided:
///   - Emits EntityAdded when a StructuralTag entity is first registered.
///   - Emits EntityMoved when a StructuralTag entity's tile changes.
///   - Emits EntityRemoved when a StructuralTag entity is destroyed.
///
/// NPC movement never emits on the structural bus — only StructuralTag entities do.
///
/// Tile coordinates: (int)(Math.Round(pos.X)), (int)(Math.Round(pos.Z)).
/// </summary>
public sealed class SpatialIndexSyncSystem : ISystem
{
    private readonly ISpatialIndex        _index;
    private readonly StructuralChangeBus? _structuralBus;

    // entity → last-synced tile position; presence in this dict means "registered"
    private readonly Dictionary<Entity, (int x, int y)> _lastPos = new();
    private long _tick;

    public SpatialIndexSyncSystem(ISpatialIndex index, StructuralChangeBus? structuralBus = null)
    {
        _index         = index;
        _structuralBus = structuralBus;
    }

    /// <summary>
    /// Called by SimulationBootstrapper when EntityManager.EntityDestroyed fires.
    /// Removes the entity from the spatial index immediately.
    /// </summary>
    public void OnEntityDestroyed(Entity entity)
    {
        if (_lastPos.TryGetValue(entity, out var last))
        {
            if (_structuralBus != null && entity.Has<StructuralTag>())
            {
                _structuralBus.Emit(StructuralChangeKind.EntityRemoved, entity.Id,
                    last.x, last.y, last.x, last.y, Guid.Empty, _tick);
            }
            _index.Unregister(entity);
            _lastPos.Remove(entity);
        }
    }

    public void Update(EntityManager em, float deltaTime)
    {
        _tick++;

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

                if (_structuralBus != null && entity.Has<StructuralTag>())
                {
                    _structuralBus.Emit(StructuralChangeKind.EntityAdded, entity.Id,
                        tx, ty, tx, ty, Guid.Empty, _tick);
                }
            }
            else if (last.x != tx || last.y != ty)
            {
                // Position changed — update cell
                _index.Update(entity, tx, ty);

                if (_structuralBus != null && entity.Has<StructuralTag>())
                {
                    _structuralBus.Emit(StructuralChangeKind.EntityMoved, entity.Id,
                        last.x, last.y, tx, ty, Guid.Empty, _tick);
                }

                _lastPos[entity] = (tx, ty);
            }
        }
    }
}
