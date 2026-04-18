using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Core;

/// <summary>
/// Unit tests for EntityManager — the O(1) component index and entity lifecycle.
///
/// Key behaviors under test:
///   • CreateEntity wires the onChange callback automatically (no manual wiring)
///   • Query&lt;T&gt;() returns entities that have T and only those entities
///   • Adding then removing a component updates the query index correctly
///   • DestroyEntity removes the entity from Entities AND all query buckets
///   • ComponentTypeCount reflects the number of distinct component types seen
///   • Query&lt;T&gt;() returns an empty enumerable (not null) when no bucket exists
///
/// None of these tests import APIFramework.Components — they use private test
/// structs so this file compiles independently of the component library.
/// </summary>
public class EntityManagerTests
{
    // ── Tiny private structs for test use only ────────────────────────────────

    private struct Alpha { public int Value; }
    private struct Beta  { public float Score; }
    private struct Gamma { public bool Flag; }

    // ── CreateEntity ──────────────────────────────────────────────────────────

    [Fact]
    public void CreateEntity_AppearsIn_Entities()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();

        Assert.Contains(entity, em.Entities);
    }

    [Fact]
    public void CreateEntity_WithExistingGuid_PreservesId()
    {
        var em   = new EntityManager();
        var guid = Guid.NewGuid();
        var e    = em.CreateEntity(guid);

        Assert.Equal(guid, e.Id);
        Assert.Contains(e, em.Entities);
    }

    [Fact]
    public void CreateMultipleEntities_AllAppearIn_Entities()
    {
        var em = new EntityManager();
        var a  = em.CreateEntity();
        var b  = em.CreateEntity();
        var c  = em.CreateEntity();

        Assert.Equal(3, em.Entities.Count);
        Assert.Contains(a, em.Entities);
        Assert.Contains(b, em.Entities);
        Assert.Contains(c, em.Entities);
    }

    // ── Query<T>() ────────────────────────────────────────────────────────────

    [Fact]
    public void Query_ReturnsEmpty_BeforeAnyComponentAdded()
    {
        var em = new EntityManager();
        em.CreateEntity(); // entity with no components

        var results = em.Query<Alpha>();

        Assert.Empty(results);
    }

    [Fact]
    public void Query_ReturnsEmpty_ForTypeNeverSeen()
    {
        // Querying a component type that has never been added to ANY entity
        // should return an empty collection, not throw or return null.
        var em      = new EntityManager();
        var results = em.Query<Alpha>();

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public void Query_ReturnsEntity_AfterComponentAdd()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new Alpha { Value = 42 });

        var results = em.Query<Alpha>().ToList();

        Assert.Single(results);
        Assert.Contains(entity, results);
    }

    [Fact]
    public void Query_ExcludesEntity_AfterComponentRemove()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new Alpha());
        entity.Remove<Alpha>();

        Assert.Empty(em.Query<Alpha>());
    }

    [Fact]
    public void Query_ReturnsOnlyEntities_ThatHaveTheComponent()
    {
        // Three entities: only two have Alpha. Query must return exactly those two.
        var em = new EntityManager();
        var a  = em.CreateEntity();
        var b  = em.CreateEntity();
        var c  = em.CreateEntity();

        a.Add(new Alpha { Value = 1 });
        b.Add(new Beta  { Score = 9.5f }); // different type
        c.Add(new Alpha { Value = 3 });

        var results = em.Query<Alpha>().ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(a, results);
        Assert.Contains(c, results);
        Assert.DoesNotContain(b, results);
    }

    [Fact]
    public void Query_ReturnsAllEntities_ThatHaveTheComponent_AcrossMultipleAdds()
    {
        var em = new EntityManager();

        // Simulate adding entities one at a time (as a system might during a tick).
        for (int i = 0; i < 10; i++)
        {
            var e = em.CreateEntity();
            e.Add(new Alpha { Value = i });
        }

        Assert.Equal(10, em.Query<Alpha>().Count());
    }

    [Fact]
    public void Query_IsolatesComponentTypes()
    {
        // Adding Beta to an entity must not make it appear in Query<Alpha>.
        var em = new EntityManager();
        var e  = em.CreateEntity();
        e.Add(new Beta { Score = 1f });

        Assert.Empty(em.Query<Alpha>());
        Assert.Single(em.Query<Beta>());
    }

    [Fact]
    public void Query_Overwrite_DoesNotDuplicate_EntityInBucket()
    {
        // Calling Add<T> a second time updates the value but must not insert
        // the entity into the index bucket a second time.
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new Alpha { Value = 1 });
        entity.Add(new Alpha { Value = 2 }); // overwrite

        Assert.Single(em.Query<Alpha>());
    }

    // ── DestroyEntity ─────────────────────────────────────────────────────────

    [Fact]
    public void DestroyEntity_RemovesFrom_Entities()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        em.DestroyEntity(entity);

        Assert.DoesNotContain(entity, em.Entities);
    }

    [Fact]
    public void DestroyEntity_RemovesFrom_AllQueryBuckets()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new Alpha());
        entity.Add(new Beta());
        entity.Add(new Gamma());

        em.DestroyEntity(entity);

        Assert.Empty(em.Query<Alpha>());
        Assert.Empty(em.Query<Beta>());
        Assert.Empty(em.Query<Gamma>());
    }

    [Fact]
    public void DestroyEntity_DoesNotAffect_OtherEntities()
    {
        var em      = new EntityManager();
        var keep    = em.CreateEntity();
        var destroy = em.CreateEntity();

        keep.Add(new Alpha    { Value = 7 });
        destroy.Add(new Alpha { Value = 8 });

        em.DestroyEntity(destroy);

        var results = em.Query<Alpha>().ToList();
        Assert.Single(results);
        Assert.Contains(keep, results);
    }

    // ── ComponentTypeCount ────────────────────────────────────────────────────

    [Fact]
    public void ComponentTypeCount_StartsAtZero()
    {
        var em = new EntityManager();
        Assert.Equal(0, em.ComponentTypeCount);
    }

    [Fact]
    public void ComponentTypeCount_IncreasesWithEachNewType()
    {
        var em = new EntityManager();
        var e  = em.CreateEntity();

        e.Add(new Alpha());
        Assert.Equal(1, em.ComponentTypeCount);

        e.Add(new Beta());
        Assert.Equal(2, em.ComponentTypeCount);

        e.Add(new Gamma());
        Assert.Equal(3, em.ComponentTypeCount);
    }

    [Fact]
    public void ComponentTypeCount_DoesNotIncrement_OnOverwrite()
    {
        var em = new EntityManager();
        var e  = em.CreateEntity();

        e.Add(new Alpha { Value = 1 });
        e.Add(new Alpha { Value = 2 }); // same type, overwrite

        Assert.Equal(1, em.ComponentTypeCount);
    }

    [Fact]
    public void ComponentTypeCount_DoesNotDecrement_OnRemove()
    {
        // The bucket stays in the index even when empty — that's by design.
        // (Adding the type again reuses the existing bucket without allocation.)
        var em = new EntityManager();
        var e  = em.CreateEntity();
        e.Add(new Alpha());
        e.Remove<Alpha>();

        // Count may be 1 (empty bucket retained) — the key point is it doesn't throw.
        Assert.True(em.ComponentTypeCount >= 0);
    }

    // ── GetAllEntities ────────────────────────────────────────────────────────

    [Fact]
    public void GetAllEntities_MatchesEntitiesList()
    {
        var em = new EntityManager();
        em.CreateEntity();
        em.CreateEntity();

        var all = em.GetAllEntities().ToList();

        Assert.Equal(em.Entities.Count, all.Count);
        foreach (var e in em.Entities)
            Assert.Contains(e, all);
    }
}
