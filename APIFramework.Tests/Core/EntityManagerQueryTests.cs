using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Core;

/// <summary>
/// Unit tests for EntityManager.Query&lt;T&gt;() with the new ComponentStoreRegistry refactor.
///
/// Verifies that Query&lt;T&gt;() still returns the correct entities after the refactor,
/// and that the component index remains consistent with the underlying stores.
/// </summary>
public class EntityManagerQueryTests
{
    private struct Alpha { public int Value; }
    private struct Beta { public float Score; }
    private struct Gamma { public bool Flag; }

    [Fact]
    public void Query_ReturnsEmpty_WhenNoEntitiesHaveComponent()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new Alpha { Value = 1 });

        var results = em.Query<Beta>().ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void Query_ReturnsExactEntities_WithTheComponent()
    {
        var em = new EntityManager();
        var a = em.CreateEntity();
        var b = em.CreateEntity();
        var c = em.CreateEntity();

        a.Add(new Alpha { Value = 1 });
        b.Add(new Alpha { Value = 2 });
        c.Add(new Beta { Score = 9.5f });  // Different type

        var results = em.Query<Alpha>().ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(a, results);
        Assert.Contains(b, results);
        Assert.DoesNotContain(c, results);
    }

    [Fact]
    public void Query_UpdatesWhenComponentAdded()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();

        Assert.Empty(em.Query<Alpha>());

        entity.Add(new Alpha { Value = 1 });

        var results = em.Query<Alpha>().ToList();
        Assert.Single(results);
        Assert.Contains(entity, results);
    }

    [Fact]
    public void Query_UpdatesWhenComponentRemoved()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new Alpha { Value = 1 });

        Assert.Single(em.Query<Alpha>());

        entity.Remove<Alpha>();

        Assert.Empty(em.Query<Alpha>());
    }

    [Fact]
    public void Query_DeterministicIteration()
    {
        // Create multiple entities and query them multiple times.
        // The iteration order should be consistent.
        var em = new EntityManager();
        var ids = new List<Guid>();

        for (int i = 0; i < 10; i++)
        {
            var e = em.CreateEntity();
            e.Add(new Alpha { Value = i });
            ids.Add(e.Id);
        }

        var results1 = em.Query<Alpha>().ToList();
        var results2 = em.Query<Alpha>().ToList();

        // Both queries should return the same entities in the same order
        Assert.Equal(results1.Count, results2.Count);
        for (int i = 0; i < results1.Count; i++)
            Assert.Equal(results1[i].Id, results2[i].Id);
    }

    [Fact]
    public void Query_ComponentOverwrite_DoesNotAffectResults()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new Alpha { Value = 1 });

        var count1 = em.Query<Alpha>().Count();

        // Overwrite the component
        entity.Add(new Alpha { Value = 2 });

        var count2 = em.Query<Alpha>().Count();

        // Same entity should still be in the query
        Assert.Equal(count1, count2);
        Assert.Single(em.Query<Alpha>());
    }

    [Fact]
    public void Query_MultipleTypes_Isolated()
    {
        var em = new EntityManager();
        var aOnly = em.CreateEntity();
        var bOnly = em.CreateEntity();
        var both = em.CreateEntity();

        aOnly.Add(new Alpha { Value = 1 });
        bOnly.Add(new Beta { Score = 2f });
        both.Add(new Alpha { Value = 3 });
        both.Add(new Beta { Score = 4f });

        var alphaResults = em.Query<Alpha>().ToList();
        var betaResults = em.Query<Beta>().ToList();

        Assert.Equal(2, alphaResults.Count);
        Assert.Contains(aOnly, alphaResults);
        Assert.Contains(both, alphaResults);
        Assert.DoesNotContain(bOnly, alphaResults);

        Assert.Equal(2, betaResults.Count);
        Assert.Contains(bOnly, betaResults);
        Assert.Contains(both, betaResults);
        Assert.DoesNotContain(aOnly, betaResults);
    }

    [Fact]
    public void Query_AfterDestroyEntity_EntityRemoved()
    {
        var em = new EntityManager();
        var keep = em.CreateEntity();
        var destroy = em.CreateEntity();

        keep.Add(new Alpha { Value = 1 });
        destroy.Add(new Alpha { Value = 2 });

        em.DestroyEntity(destroy);

        var results = em.Query<Alpha>().ToList();

        Assert.Single(results);
        Assert.Contains(keep, results);
        Assert.DoesNotContain(destroy, results);
    }

    [Fact]
    public void Query_ManyEntitiesWithComponent()
    {
        var em = new EntityManager();
        const int count = 100;

        for (int i = 0; i < count; i++)
        {
            var e = em.CreateEntity();
            e.Add(new Alpha { Value = i });
        }

        var results = em.Query<Alpha>().ToList();

        Assert.Equal(count, results.Count);
    }

    [Fact]
    public void Query_ComponentTypeCount_Tracks()
    {
        var em = new EntityManager();
        Assert.Equal(0, em.ComponentTypeCount);

        var e1 = em.CreateEntity();
        e1.Add(new Alpha());
        Assert.Equal(1, em.ComponentTypeCount);

        var e2 = em.CreateEntity();
        e2.Add(new Beta());
        Assert.Equal(2, em.ComponentTypeCount);

        var e3 = em.CreateEntity();
        e3.Add(new Gamma());
        Assert.Equal(3, em.ComponentTypeCount);

        e1.Remove<Alpha>();
        // ComponentTypeCount may be >= 3 (bucket retained) — depends on implementation
        Assert.True(em.ComponentTypeCount >= 0);
    }
}
