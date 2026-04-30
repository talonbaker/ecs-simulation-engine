using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>AT-04: SpatialIndexSyncSystem register / update / unregister lifecycle.</summary>
public class SpatialIndexSyncSystemTests
{
    private static (EntityManager em, GridSpatialIndex idx, SpatialIndexSyncSystem sys) Setup()
    {
        var em      = new EntityManager();
        var idx     = new GridSpatialIndex(4, 128, 128);
        var structBus = new StructuralChangeBus();
        var sys     = new SpatialIndexSyncSystem(idx, structBus);
        em.EntityDestroyed += sys.OnEntityDestroyed;
        return (em, idx, sys);
    }

    [Fact]
    public void NewEntity_RegisteredAfterOneTick()
    {
        var (em, idx, sys) = Setup();
        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 10f, Y = 0f, Z = 20f });

        sys.Update(em, 1f);

        var results = idx.QueryRadius(10, 20, 1);
        Assert.Contains(entity, results);
    }

    [Fact]
    public void MovedEntity_UpdatesWithinOneTick()
    {
        var (em, idx, sys) = Setup();
        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 10f, Y = 0f, Z = 10f });

        sys.Update(em, 1f);

        // Move to (50, 50)
        entity.Add(new PositionComponent { X = 50f, Y = 0f, Z = 50f });
        sys.Update(em, 1f);

        Assert.DoesNotContain(entity, idx.QueryRadius(10, 10, 1));
        Assert.Contains(entity, idx.QueryRadius(50, 50, 1));
    }

    [Fact]
    public void DestroyedEntity_UnregisteredWithinOneTick()
    {
        var (em, idx, sys) = Setup();
        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 10f, Y = 0f, Z = 10f });

        sys.Update(em, 1f);
        Assert.Contains(entity, idx.QueryRadius(10, 10, 1));

        em.DestroyEntity(entity);

        // Destruction fires the event synchronously — index is cleared immediately
        Assert.DoesNotContain(entity, idx.QueryRadius(10, 10, 1));
    }

    [Fact]
    public void MultipleEntities_AllRegisteredAfterOneTick()
    {
        var (em, idx, sys) = Setup();
        var e1 = em.CreateEntity(); e1.Add(new PositionComponent { X = 5f,  Y = 0f, Z = 5f  });
        var e2 = em.CreateEntity(); e2.Add(new PositionComponent { X = 50f, Y = 0f, Z = 50f });

        sys.Update(em, 1f);

        Assert.Contains(e1, idx.QueryRadius(5,  5,  1));
        Assert.Contains(e2, idx.QueryRadius(50, 50, 1));
    }

    [Fact]
    public void StaticEntity_NoUpdateOnSubsequentTicks()
    {
        var (em, idx, sys) = Setup();
        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 10f, Y = 0f, Z = 10f });

        sys.Update(em, 1f);
        sys.Update(em, 1f); // second tick, no movement

        // Still registered in exactly one place
        var results = idx.QueryRadius(10, 10, 1);
        Assert.Single(results);
        Assert.Contains(entity, results);
    }
}
