using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Spatial;

namespace APIFramework.Mutation;

/// <summary>
/// Default implementation of IWorldMutationApi.
/// Every method validates inputs, applies the mutation through the entity manager,
/// and emits the corresponding StructuralChangeEvent on the bus.
/// </summary>
public sealed class WorldMutationApi : IWorldMutationApi
{
    private readonly EntityManager      _em;
    private readonly StructuralChangeBus _bus;
    private long _seq;

    public WorldMutationApi(EntityManager em, StructuralChangeBus bus)
    {
        _em  = em;
        _bus = bus;
    }

    /// <inheritdoc/>
    public void MoveEntity(Guid entityId, int newTileX, int newTileY)
    {
        var entity = FindById(entityId)
            ?? throw new InvalidOperationException($"Entity {entityId} not found.");

        if (!entity.Has<MutableTopologyTag>())
            throw new InvalidOperationException(
                $"Entity {entityId} does not have MutableTopologyTag and cannot be moved via IWorldMutationApi.");

        int prevX = 0, prevY = 0;
        if (entity.Has<PositionComponent>())
        {
            var pos = entity.Get<PositionComponent>();
            prevX = (int)Math.Round(pos.X);
            prevY = (int)Math.Round(pos.Z);
        }

        entity.Add(new PositionComponent { X = newTileX, Y = 0f, Z = newTileY });

        _bus.Emit(StructuralChangeKind.EntityMoved, entityId,
            prevX, prevY, newTileX, newTileY, Guid.Empty, ++_seq);
    }

    /// <inheritdoc/>
    public Guid SpawnStructural(int tileX, int tileY)
    {
        var entity = _em.CreateEntity();
        entity.Add(default(StructuralTag));
        entity.Add(default(MutableTopologyTag));
        entity.Add(default(ObstacleTag));
        entity.Add(new PositionComponent { X = tileX, Y = 0f, Z = tileY });

        _bus.Emit(StructuralChangeKind.EntityAdded, entity.Id,
            tileX, tileY, tileX, tileY, Guid.Empty, ++_seq);

        return entity.Id;
    }

    /// <inheritdoc/>
    public void DespawnStructural(Guid entityId)
    {
        var entity = FindById(entityId)
            ?? throw new InvalidOperationException($"Entity {entityId} not found.");

        int tileX = 0, tileY = 0;
        if (entity.Has<PositionComponent>())
        {
            var pos = entity.Get<PositionComponent>();
            tileX = (int)Math.Round(pos.X);
            tileY = (int)Math.Round(pos.Z);
        }

        // Emit before destruction so subscribers can read entity state if needed
        _bus.Emit(StructuralChangeKind.EntityRemoved, entityId,
            tileX, tileY, tileX, tileY, Guid.Empty, ++_seq);

        _em.DestroyEntity(entity);
    }

    /// <inheritdoc/>
    public void AttachObstacle(Guid entityId)
    {
        var entity = FindById(entityId)
            ?? throw new InvalidOperationException($"Entity {entityId} not found.");

        if (!entity.Has<ObstacleTag>())
            entity.Add(default(ObstacleTag));
        if (!entity.Has<StructuralTag>())
            entity.Add(default(StructuralTag));

        int tileX = 0, tileY = 0;
        if (entity.Has<PositionComponent>())
        {
            var pos = entity.Get<PositionComponent>();
            tileX = (int)Math.Round(pos.X);
            tileY = (int)Math.Round(pos.Z);
        }

        _bus.Emit(StructuralChangeKind.ObstacleAttached, entityId,
            tileX, tileY, tileX, tileY, Guid.Empty, ++_seq);
    }

    /// <inheritdoc/>
    public void DetachObstacle(Guid entityId)
    {
        var entity = FindById(entityId)
            ?? throw new InvalidOperationException($"Entity {entityId} not found.");

        entity.Remove<ObstacleTag>();

        int tileX = 0, tileY = 0;
        if (entity.Has<PositionComponent>())
        {
            var pos = entity.Get<PositionComponent>();
            tileX = (int)Math.Round(pos.X);
            tileY = (int)Math.Round(pos.Z);
        }

        _bus.Emit(StructuralChangeKind.ObstacleDetached, entityId,
            tileX, tileY, tileX, tileY, Guid.Empty, ++_seq);
    }

    /// <inheritdoc/>
    public void ChangeRoomBounds(Guid roomId, BoundsRect newBounds)
    {
        var entity = FindById(roomId)
            ?? throw new InvalidOperationException($"Room entity {roomId} not found.");

        if (!entity.Has<RoomComponent>())
            throw new InvalidOperationException($"Entity {roomId} does not have RoomComponent.");

        var room = entity.Get<RoomComponent>();
        entity.Add(room with { Bounds = newBounds });

        _bus.Emit(StructuralChangeKind.RoomBoundsChanged, roomId,
            room.Bounds.X, room.Bounds.Y, newBounds.X, newBounds.Y, roomId, ++_seq);
    }

    private Entity? FindById(Guid id) =>
        _em.GetAllEntities().FirstOrDefault(e => e.Id == id);
}
