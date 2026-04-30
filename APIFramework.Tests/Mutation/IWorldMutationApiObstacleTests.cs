using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Mutation;

/// <summary>
/// AT-08: AttachObstacle emits ObstacleAttached; DetachObstacle emits ObstacleDetached.
/// </summary>
public class IWorldMutationApiObstacleTests
{
    [Fact]
    public void AttachObstacle_AddsTagsAndEmitsEvent()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 3f, Y = 0f, Z = 4f });

        StructuralChangeEvent? evt = null;
        bus.Subscribe(e => evt = e);

        api.AttachObstacle(entity.Id);

        Assert.True(entity.Has<ObstacleTag>());
        Assert.True(entity.Has<StructuralTag>());
        Assert.NotNull(evt);
        Assert.Equal(StructuralChangeKind.ObstacleAttached, evt!.Value.Kind);
        Assert.Equal(entity.Id, evt.Value.EntityId);
        Assert.Equal(1L, bus.TopologyVersion);
    }

    [Fact]
    public void DetachObstacle_RemovesTagAndEmitsEvent()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        var entity = em.CreateEntity();
        entity.Add(default(ObstacleTag));
        entity.Add(default(StructuralTag));
        entity.Add(new PositionComponent { X = 5f, Y = 0f, Z = 6f });

        StructuralChangeEvent? evt = null;
        bus.Subscribe(e => evt = e);

        api.DetachObstacle(entity.Id);

        Assert.False(entity.Has<ObstacleTag>());
        Assert.NotNull(evt);
        Assert.Equal(StructuralChangeKind.ObstacleDetached, evt!.Value.Kind);
        Assert.Equal(entity.Id, evt.Value.EntityId);
    }

    [Fact]
    public void AttachObstacle_Idempotent_DoesNotDuplicateTags()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 1f, Y = 0f, Z = 1f });

        api.AttachObstacle(entity.Id);
        api.AttachObstacle(entity.Id);

        Assert.True(entity.Has<ObstacleTag>());
        Assert.True(entity.Has<StructuralTag>());
        Assert.Equal(2L, bus.TopologyVersion);  // two emits, both valid
    }

    [Fact]
    public void AttachObstacle_NonexistentEntity_Throws()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        Assert.Throws<InvalidOperationException>(() => api.AttachObstacle(Guid.NewGuid()));
    }

    [Fact]
    public void DetachObstacle_NonexistentEntity_Throws()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        Assert.Throws<InvalidOperationException>(() => api.DetachObstacle(Guid.NewGuid()));
    }
}
