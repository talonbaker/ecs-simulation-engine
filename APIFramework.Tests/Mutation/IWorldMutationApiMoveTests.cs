using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Mutation;

/// <summary>
/// AT-06 / AT-07: MoveEntity semantics and MutableTopologyTag enforcement.
/// </summary>
public class IWorldMutationApiMoveTests
{
    private static (EntityManager em, StructuralChangeBus bus, WorldMutationApi api) Setup()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);
        return (em, bus, api);
    }

    // AT-06: MoveEntity on a tagged entity updates PositionComponent, emits EntityMoved,
    //        increments TopologyVersion, and clears the cache.
    [Fact]
    public void MoveEntity_TaggedEntity_UpdatesPositionAndEmitsEvent()
    {
        var (em, bus, api) = Setup();

        var entity = em.CreateEntity();
        entity.Add(default(StructuralTag));
        entity.Add(default(MutableTopologyTag));
        entity.Add(new PositionComponent { X = 2f, Y = 0f, Z = 3f });

        StructuralChangeEvent? evt = null;
        bus.Subscribe(e => evt = e);

        api.MoveEntity(entity.Id, 5, 7);

        var pos = entity.Get<PositionComponent>();
        Assert.Equal(5f, pos.X);
        Assert.Equal(7f, pos.Z);

        Assert.NotNull(evt);
        Assert.Equal(StructuralChangeKind.EntityMoved, evt!.Value.Kind);
        Assert.Equal(entity.Id, evt.Value.EntityId);
        Assert.Equal(2, evt.Value.PreviousTileX);
        Assert.Equal(3, evt.Value.PreviousTileY);
        Assert.Equal(5, evt.Value.CurrentTileX);
        Assert.Equal(7, evt.Value.CurrentTileY);
        Assert.Equal(1L, bus.TopologyVersion);
    }

    [Fact]
    public void MoveEntity_TaggedEntity_IncreasesTopologyVersion()
    {
        var (em, bus, api) = Setup();

        var entity = em.CreateEntity();
        entity.Add(default(StructuralTag));
        entity.Add(default(MutableTopologyTag));
        entity.Add(new PositionComponent { X = 0f, Y = 0f, Z = 0f });

        long versionBefore = bus.TopologyVersion;
        api.MoveEntity(entity.Id, 3, 3);

        Assert.True(bus.TopologyVersion > versionBefore);
    }

    // AT-07: MoveEntity on entity without MutableTopologyTag throws; state is unchanged; no event.
    [Fact]
    public void MoveEntity_NoMutableTopologyTag_ThrowsAndEmitsNothing()
    {
        var (em, bus, api) = Setup();

        var entity = em.CreateEntity();
        entity.Add(default(StructuralTag));  // no MutableTopologyTag
        entity.Add(new PositionComponent { X = 1f, Y = 0f, Z = 1f });

        var events = new List<StructuralChangeEvent>();
        bus.Subscribe(e => events.Add(e));

        Assert.Throws<InvalidOperationException>(() => api.MoveEntity(entity.Id, 5, 5));

        // State unchanged
        var pos = entity.Get<PositionComponent>();
        Assert.Equal(1f, pos.X);
        Assert.Equal(1f, pos.Z);

        // No event emitted
        Assert.Empty(events);
        Assert.Equal(0L, bus.TopologyVersion);
    }

    [Fact]
    public void MoveEntity_NonexistentEntity_Throws()
    {
        var (em, bus, api) = Setup();
        Assert.Throws<InvalidOperationException>(() => api.MoveEntity(Guid.NewGuid(), 1, 1));
    }
}
