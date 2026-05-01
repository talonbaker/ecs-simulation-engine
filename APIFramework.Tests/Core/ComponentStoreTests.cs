using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Core;

/// <summary>
/// Unit tests for ComponentStore&lt;T&gt; — the per-type typed storage for components.
///
/// Key behaviors under test:
///   • Get / Has / Set / Add / Remove semantics
///   • EntityIds enumeration
///   • Count property
///   • Overwriting via Set (idempotent)
///   • Add throws when component already exists
///   • Remove is idempotent (works whether component exists or not)
/// </summary>
public class ComponentStoreTests
{
    private struct TestComponent { public int Value; }
    private struct AnotherComponent { public string Name; }

    [Fact]
    public void Get_ReturnsExactValueStored()
    {
        var store = new ComponentStore<TestComponent>();
        var id = Guid.NewGuid();
        var value = new TestComponent { Value = 42 };

        store.Add(id, value);
        var retrieved = store.Get(id);

        Assert.Equal(42, retrieved.Value);
    }

    [Fact]
    public void Get_Throws_WhenAbsent()
    {
        var store = new ComponentStore<TestComponent>();
        var id = Guid.NewGuid();

        Assert.Throws<KeyNotFoundException>(() => store.Get(id));
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenAbsent()
    {
        var store = new ComponentStore<TestComponent>();
        var id = Guid.NewGuid();

        bool found = store.TryGet(id, out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGet_ReturnsTrueAndValue_WhenPresent()
    {
        var store = new ComponentStore<TestComponent>();
        var id = Guid.NewGuid();
        var value = new TestComponent { Value = 99 };
        store.Add(id, value);

        bool found = store.TryGet(id, out var retrieved);

        Assert.True(found);
        Assert.Equal(99, retrieved.Value);
    }

    [Fact]
    public void Has_ReturnsFalse_BeforeAdd()
    {
        var store = new ComponentStore<TestComponent>();
        var id = Guid.NewGuid();

        Assert.False(store.Has(id));
    }

    [Fact]
    public void Has_ReturnsTrue_AfterAdd()
    {
        var store = new ComponentStore<TestComponent>();
        var id = Guid.NewGuid();
        store.Add(id, new TestComponent { Value = 1 });

        Assert.True(store.Has(id));
    }

    [Fact]
    public void Has_ReturnsFalse_AfterRemove()
    {
        var store = new ComponentStore<TestComponent>();
        var id = Guid.NewGuid();
        store.Add(id, new TestComponent { Value = 1 });
        store.Remove(id);

        Assert.False(store.Has(id));
    }

    [Fact]
    public void Set_Overwrites_ExistingValue()
    {
        var store = new ComponentStore<TestComponent>();
        var id = Guid.NewGuid();
        store.Add(id, new TestComponent { Value = 1 });

        store.Set(id, new TestComponent { Value = 2 });
        var retrieved = store.Get(id);

        Assert.Equal(2, retrieved.Value);
    }

    [Fact]
    public void Set_CreatesIfAbsent()
    {
        var store = new ComponentStore<TestComponent>();
        var id = Guid.NewGuid();

        // Set on a non-existent entity should succeed (not throw)
        store.Set(id, new TestComponent { Value = 5 });

        Assert.True(store.Has(id));
        Assert.Equal(5, store.Get(id).Value);
    }

    [Fact]
    public void Add_Throws_WhenAlreadyExists()
    {
        var store = new ComponentStore<TestComponent>();
        var id = Guid.NewGuid();
        store.Add(id, new TestComponent { Value = 1 });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            store.Add(id, new TestComponent { Value = 2 }));
        Assert.Contains("already has", ex.Message);
    }

    [Fact]
    public void Remove_IsIdempotent()
    {
        var store = new ComponentStore<TestComponent>();
        var id = Guid.NewGuid();
        store.Add(id, new TestComponent { Value = 1 });

        // First remove
        store.Remove(id);
        Assert.False(store.Has(id));

        // Second remove on already-absent entity should not throw
        var ex = Record.Exception(() => store.Remove(id));
        Assert.Null(ex);
    }

    [Fact]
    public void EntityIds_EnumeratesAllKeys()
    {
        var store = new ComponentStore<TestComponent>();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        store.Add(id1, new TestComponent { Value = 1 });
        store.Add(id2, new TestComponent { Value = 2 });
        store.Add(id3, new TestComponent { Value = 3 });

        var ids = store.EntityIds.ToList();

        Assert.Equal(3, ids.Count);
        Assert.Contains(id1, ids);
        Assert.Contains(id2, ids);
        Assert.Contains(id3, ids);
    }

    [Fact]
    public void EntityIds_ExcludesRemovedEntities()
    {
        var store = new ComponentStore<TestComponent>();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        store.Add(id1, new TestComponent { Value = 1 });
        store.Add(id2, new TestComponent { Value = 2 });
        store.Remove(id1);

        var ids = store.EntityIds.ToList();

        Assert.Single(ids);
        Assert.Contains(id2, ids);
        Assert.DoesNotContain(id1, ids);
    }

    [Fact]
    public void Count_TracksNumberOfStoredComponents()
    {
        var store = new ComponentStore<TestComponent>();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        Assert.Equal(0, store.Count);

        store.Add(id1, new TestComponent { Value = 1 });
        Assert.Equal(1, store.Count);

        store.Add(id2, new TestComponent { Value = 2 });
        Assert.Equal(2, store.Count);

        store.Remove(id1);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void MultipleTypes_DoNotInterfere()
    {
        var store1 = new ComponentStore<TestComponent>();
        var store2 = new ComponentStore<AnotherComponent>();
        var id = Guid.NewGuid();

        store1.Add(id, new TestComponent { Value = 42 });
        store2.Add(id, new AnotherComponent { Name = "Test" });

        Assert.True(store1.Has(id));
        Assert.True(store2.Has(id));
        Assert.Equal(42, store1.Get(id).Value);
        Assert.Equal("Test", store2.Get(id).Name);
    }
}
