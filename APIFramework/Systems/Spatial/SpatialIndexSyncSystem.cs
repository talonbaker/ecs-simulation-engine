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
/// <remarks>
/// Reads: every entity carrying <see cref="PositionComponent"/> and (optionally) <see cref="StructuralTag"/>.
/// Writes: the singleton <see cref="ISpatialIndex"/> via Register/Update/Unregister
/// (single writer of the spatial index), plus emissions on <see cref="StructuralChangeBus"/>
/// for entities that bear <see cref="StructuralTag"/>.
/// Ordering: must run before <see cref="RoomMembershipSystem"/> and any other consumer that
/// queries the spatial index in the same tick.
/// </remarks>
/// <seealso cref="ISpatialIndex"/>
/// <seealso cref="StructuralChangeBus"/>
/// <seealso cref="RoomMembershipSystem"/>
public sealed class SpatialIndexSyncSystem : ISystem
{
    private readonly ISpatialIndex _index;
    private readonly StructuralChangeBus _structuralBus;

    // entity → last-synced tile position; presence in this dict means "registered"
    private readonly Dictionary<Entity, (int x, int y)> _lastPos = new();
    private long _tickCounter = 0;

    /// <summary>
    /// Constructs the system.
    /// </summary>
    /// <param name="index">The singleton spatial index this system owns synchronization for.</param>
    /// <param name="structuralBus">Bus on which Added/Moved/Removed events are emitted for
    /// <see cref="StructuralTag"/> entities so cache-dependent subscribers can invalidate.</param>
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

    /// <summary>
    /// Per-tick scan: for every <see cref="PositionComponent"/> entity, register on first sight,
    /// update the index when the rounded tile position changes, and emit the corresponding
    /// <see cref="StructuralChangeKind"/> event when the entity bears <see cref="StructuralTag"/>.
    /// </summary>
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
