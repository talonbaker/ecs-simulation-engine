using System;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Mutation;

/// <summary>
/// AT-08: ChangeRoomBounds emits RoomBoundsChanged and updates RoomComponent.
/// </summary>
public class IWorldMutationApiRoomBoundsTests
{
    [Fact]
    public void ChangeRoomBounds_UpdatesBoundsAndEmitsEvent()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        var room = em.CreateEntity();
        room.Add(default(RoomTag));
        room.Add(new RoomComponent
        {
            Id     = room.Id.ToString(),
            Name   = "Office",
            Bounds = new BoundsRect(0, 0, 10, 10),
        });

        StructuralChangeEvent? evt = null;
        bus.Subscribe(e => evt = e);

        var newBounds = new BoundsRect(0, 0, 12, 10);
        api.ChangeRoomBounds(room.Id, newBounds);

        Assert.Equal(newBounds, room.Get<RoomComponent>().Bounds);
        Assert.NotNull(evt);
        Assert.Equal(StructuralChangeKind.RoomBoundsChanged, evt!.Value.Kind);
        Assert.Equal(room.Id, evt.Value.EntityId);
        Assert.Equal(room.Id, evt.Value.RoomId);
        Assert.Equal(1L, bus.TopologyVersion);
    }

    [Fact]
    public void ChangeRoomBounds_NonexistentRoom_Throws()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        Assert.Throws<InvalidOperationException>(() =>
            api.ChangeRoomBounds(Guid.NewGuid(), new BoundsRect(0, 0, 5, 5)));
    }

    [Fact]
    public void ChangeRoomBounds_EntityWithoutRoomComponent_Throws()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        var entity = em.CreateEntity();

        Assert.Throws<InvalidOperationException>(() =>
            api.ChangeRoomBounds(entity.Id, new BoundsRect(0, 0, 5, 5)));
    }
}
