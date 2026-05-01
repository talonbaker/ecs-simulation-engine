using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Mutation;

/// <summary>AT-09: IWorldMutationApi.ThrowEntity attaches ThrownVelocityComponent + ThrownTag.</summary>
public class IWorldMutationApiThrowEntityTests
{
    private static (WorldMutationApi api, EntityManager em, Entity entity) Build()
    {
        var em     = new EntityManager();
        var bus    = new StructuralChangeBus();
        var api    = new WorldMutationApi(em, bus);
        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        return (api, em, entity);
    }

    [Fact]
    public void AT09_ThrowEntity_AttachesThrownVelocityComponent()
    {
        var (api, em, entity) = Build();

        api.ThrowEntity(entity.Id, 5f, 0f, 3f, 0.10f);

        Assert.True(entity.Has<ThrownVelocityComponent>());
    }

    [Fact]
    public void AT09_ThrowEntity_AttachesThrownTag()
    {
        var (api, em, entity) = Build();

        api.ThrowEntity(entity.Id, 5f, 0f, 3f, 0.10f);

        Assert.True(entity.Has<ThrownTag>());
    }

    [Fact]
    public void AT09_ThrowEntity_VelocityFieldsCorrect()
    {
        var (api, em, entity) = Build();

        api.ThrowEntity(entity.Id, 4f, 2f, 1.5f, 0.05f);

        var v = entity.Get<ThrownVelocityComponent>();
        Assert.Equal(4f, v.VelocityX);
        Assert.Equal(2f, v.VelocityZ);
        Assert.Equal(1.5f, v.VelocityY);
        Assert.Equal(0.05f, v.DecayPerTick);
    }

    [Fact]
    public void AT09_ThrowEntity_NonexistentId_Throws()
    {
        var (api, em, entity) = Build();

        Assert.Throws<InvalidOperationException>(() =>
            api.ThrowEntity(Guid.NewGuid(), 1f, 0f, 0f, 0.1f));
    }

    [Fact]
    public void SpawnStain_WaterPuddle_HasStainAndFallRisk()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        var id = api.SpawnStain("water-puddle", 3, 7);

        var entity = em.GetAllEntities().First(e => e.Id == id);
        Assert.True(entity.Has<StainTag>());
        Assert.True(entity.Has<FallRiskComponent>());
        Assert.True(entity.Has<StainComponent>());
    }

    [Fact]
    public void SpawnStain_BrokenGlass_HasHigherFallRisk()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);

        var waterId  = api.SpawnStain("water-puddle", 3, 7);
        var glassId  = api.SpawnStain("broken-glass", 4, 7);

        var water = em.GetAllEntities().First(e => e.Id == waterId);
        var glass = em.GetAllEntities().First(e => e.Id == glassId);

        Assert.True(glass.Get<FallRiskComponent>().RiskLevel > water.Get<FallRiskComponent>().RiskLevel);
    }
}
