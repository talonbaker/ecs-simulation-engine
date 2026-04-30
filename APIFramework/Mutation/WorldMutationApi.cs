using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Spatial;

namespace APIFramework.Mutation;

/// <summary>
/// Default implementation of IWorldMutationApi.
/// Applies the mutation to the entity manager and emits the corresponding structural change event.
///
/// Boot-time spawns (via SpawnWorld) do NOT use this API — they happen before the bus has subscribers.
/// Documented in the completion note.
/// </summary>
public sealed class WorldMutationApi : IWorldMutationApi
{
    private readonly EntityManager _em;
    private readonly StructuralChangeBus _bus;
    private long _tickCounter = 0;

    public WorldMutationApi(EntityManager em, StructuralChangeBus bus)
    {
        _em = em;
        _bus = bus;
    }

    private Entity? FindEntityById(Guid id)
    {
        foreach (var entity in _em.GetAllEntities())
        {
            if (entity.Id == id)
                return entity;
        }
        return null;
    }

    public bool MoveEntity(Guid entityId, int newTileX, int newTileY)
    {
        var entity = FindEntityById(entityId);
        if (entity == null)
            return false;

        // Verify the entity has MutableTopologyTag
        if (!entity.Has<MutableTopologyTag>())
            return false;

        // Must have PositionComponent
        if (!entity.Has<PositionComponent>())
            return false;

        // Get previous position
        var pos = entity.Get<PositionComponent>();
        int prevX = (int)System.Math.Round(pos.X);
        int prevY = (int)System.Math.Round(pos.Z);

        // Only emit if position actually changed
        if (prevX == newTileX && prevY == newTileY)
            return true;  // No-op success

        // Update position
        var newPos = pos;
        newPos.X = newTileX;
        newPos.Z = newTileY;
        entity.Add(newPos);

        // Get room ID
        Guid roomId = Guid.Empty;

        // Emit the change
        _bus.Emit(
            StructuralChangeKind.EntityMoved,
            entityId,
            prevX, prevY,
            newTileX, newTileY,
            roomId,
            _tickCounter++
        );

        return true;
    }

    public Guid SpawnStructural(Guid templateId, int tileX, int tileY)
    {
        // This is a simplified spawn. A real implementation would use a template system.
        // For now, fail closed.
        return Guid.Empty;
    }

    public bool DespawnStructural(Guid entityId)
    {
        var entity = FindEntityById(entityId);
        if (entity == null)
            return false;

        // Verify it has StructuralTag
        if (!entity.Has<StructuralTag>())
            return false;

        // Get position before despawn
        int tileX = 0, tileY = 0;
        if (entity.Has<PositionComponent>())
        {
            var pos = entity.Get<PositionComponent>();
            tileX = (int)System.Math.Round(pos.X);
            tileY = (int)System.Math.Round(pos.Z);
        }

        // Get room ID
        Guid roomId = Guid.Empty;

        // Destroy the entity
        _em.DestroyEntity(entity);

        // Emit the change
        _bus.Emit(
            StructuralChangeKind.EntityRemoved,
            entityId,
            tileX, tileY,
            tileX, tileY,
            roomId,
            _tickCounter++
        );

        return true;
    }

    public bool AttachObstacle(Guid entityId)
    {
        var entity = FindEntityById(entityId);
        if (entity == null)
            return false;

        // Don't re-attach if already present
        if (entity.Has<ObstacleTag>())
            return true;

        // Attach
        entity.Add(new ObstacleTag());

        // Get position for the event
        int tileX = 0, tileY = 0;
        if (entity.Has<PositionComponent>())
        {
            var pos = entity.Get<PositionComponent>();
            tileX = (int)System.Math.Round(pos.X);
            tileY = (int)System.Math.Round(pos.Z);
        }

        // Emit
        _bus.Emit(
            StructuralChangeKind.ObstacleAttached,
            entityId,
            tileX, tileY,
            tileX, tileY,
            Guid.Empty,
            _tickCounter++
        );

        return true;
    }

    public bool DetachObstacle(Guid entityId)
    {
        var entity = FindEntityById(entityId);
        if (entity == null)
            return false;

        // Nothing to do if not attached
        if (!entity.Has<ObstacleTag>())
            return true;

        // Remove
        entity.Remove<ObstacleTag>();

        // Get position for the event
        int tileX = 0, tileY = 0;
        if (entity.Has<PositionComponent>())
        {
            var pos = entity.Get<PositionComponent>();
            tileX = (int)System.Math.Round(pos.X);
            tileY = (int)System.Math.Round(pos.Z);
        }

        // Emit
        _bus.Emit(
            StructuralChangeKind.ObstacleDetached,
            entityId,
            tileX, tileY,
            tileX, tileY,
            Guid.Empty,
            _tickCounter++
        );

        return true;
    }

    public bool ChangeRoomBounds(Guid roomId, BoundsRect newBounds)
    {
        var roomEntity = FindEntityById(roomId);
        if (roomEntity == null)
            return false;

        if (!roomEntity.Has<RoomComponent>())
            return false;

        // Update bounds
        var room = roomEntity.Get<RoomComponent>();
        room.Bounds = newBounds;
        roomEntity.Add(room);

        // Emit
        _bus.Emit(
            StructuralChangeKind.RoomBoundsChanged,
            roomId,
            0, 0,
            0, 0,
            roomId,
            _tickCounter++
        );

        return true;
    }
}
