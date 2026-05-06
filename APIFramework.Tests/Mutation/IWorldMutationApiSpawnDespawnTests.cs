using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Mutation;

/// <summary>
/// AT-08: SpawnStructural emits EntityAdded; DespawnStructural emits EntityRemoved.
/// Both increment TopologyVersion.
/// </summary>
public class IWorldMutationApiSpawnDespawnTests
{
    [Fact]
    public void SpawnStructural_EmitsEntityAdded_IncreasesTopologyVersion()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        StructuralChangeEvent? evt = null;
        bus.Subscribe(e => evt = e);

        var newId = api.SpawnStructural(3, 7);

        Assert.NotEqual(Guid.Empty, newId);
        Assert.NotNull(evt);
        Assert.Equal(StructuralChangeKind.EntityAdded, evt!.Value.Kind);
        Assert.Equal(newId, evt.Value.EntityId);
        Assert.Equal(3, evt.Value.CurrentTileX);
        Assert.Equal(7, evt.Value.CurrentTileY);
        Assert.Equal(1L, bus.TopologyVersion);
    }

    [Fact]
    public void SpawnStructural_NewEntity_HasStructuralAndObstacleTags()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        var newId = api.SpawnStructural(5, 5);
        var entity = em.GetAllEntities().First(e => e.Id == newId);

        Assert.True(entity.Has<StructuralTag>());
        Assert.True(entity.Has<MutableTopologyTag>());
        Assert.True(entity.Has<ObstacleTag>());
        Assert.True(entity.Has<PositionComponent>());
        Assert.Equal(5f, entity.Get<PositionComponent>().X);
        Assert.Equal(5f, entity.Get<PositionComponent>().Z);
    }

    [Fact]
    public void DespawnStructural_EmitsEntityRemoved_IncreasesTopologyVersion()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        var newId = api.SpawnStructural(4, 4);
        var versionAfterSpawn = bus.TopologyVersion;

        var events = new List<StructuralChangeEvent>();
        bus.Subscribe(e => events.Add(e));

        api.DespawnStructural(newId);

        Assert.Single(events);
        Assert.Equal(StructuralChangeKind.EntityRemoved, events[0].Kind);
        Assert.Equal(newId, events[0].EntityId);
        Assert.True(bus.TopologyVersion > versionAfterSpawn);
    }

    [Fact]
    public void DespawnStructural_EntityIsRemovedFromManager()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        var newId = api.SpawnStructural(1, 1);
        Assert.Single(em.GetAllEntities(), e => e.Id == newId);

        api.DespawnStructural(newId);

        Assert.DoesNotContain(em.GetAllEntities(), e => e.Id == newId);
    }

    [Fact]
    public void DespawnStructural_NonexistentEntity_Throws()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        Assert.Throws<InvalidOperationException>(() => api.DespawnStructural(Guid.NewGuid()));
    }
}
