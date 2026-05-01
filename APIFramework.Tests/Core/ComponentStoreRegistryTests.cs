using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Core;

/// <summary>
/// Unit tests for ComponentStoreRegistry — the central registry holding one typed store per component type.
///
/// Key behaviors under test:
///   • Store&lt;T&gt;() creates a new store on first access
///   • Store&lt;T&gt;() returns the same instance on repeated calls for the same T
///   • Different component types get different stores
///   • Cross-type dispatch is correct
/// </summary>
public class ComponentStoreRegistryTests
{
    private struct ComponentA { public int X; }
    private struct ComponentB { public float Y; }
    private struct ComponentC { public string Z; }

    [Fact]
    public void Store_CreatesNewInstance_OnFirstAccess()
    {
        var registry = new ComponentStoreRegistry();

        var store = registry.Store<ComponentA>();

        Assert.NotNull(store);
        Assert.IsType<ComponentStore<ComponentA>>(store);
    }

    [Fact]
    public void Store_ReturnsSameInstance_OnRepeatedAccess()
    {
        var registry = new ComponentStoreRegistry();

        var store1 = registry.Store<ComponentA>();
        var store2 = registry.Store<ComponentA>();

        Assert.Same(store1, store2);
    }

    [Fact]
    public void Store_ReturnsDifferentInstances_ForDifferentTypes()
    {
        var registry = new ComponentStoreRegistry();

        var storeA = registry.Store<ComponentA>();
        var storeB = registry.Store<ComponentB>();

        Assert.NotSame(storeA, storeB);
    }

    [Fact]
    public void Store_EachTypeHasIndependentStorage()
    {
        var registry = new ComponentStoreRegistry();
        var id = Guid.NewGuid();

        var storeA = registry.Store<ComponentA>();
        var storeB = registry.Store<ComponentB>();

        storeA.Add(id, new ComponentA { X = 10 });
        storeB.Add(id, new ComponentB { Y = 20f });

        Assert.True(storeA.Has(id));
        Assert.True(storeB.Has(id));
        Assert.Equal(10, storeA.Get(id).X);
        Assert.Equal(20f, storeB.Get(id).Y);
    }

    [Fact]
    public void Store_Isolation_AddingToOneDoesNotAffectAnother()
    {
        var registry = new ComponentStoreRegistry();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();

        var storeA = registry.Store<ComponentA>();
        var storeB = registry.Store<ComponentB>();

        storeA.Add(idA, new ComponentA { X = 1 });
        // Don't add idA to storeB

        Assert.True(storeA.Has(idA));
        Assert.False(storeB.Has(idA));  // Different store, different membership
    }

    [Fact]
    public void Store_MultipleTypes_AllAccessible()
    {
        var registry = new ComponentStoreRegistry();
        var id = Guid.NewGuid();

        registry.Store<ComponentA>().Add(id, new ComponentA { X = 1 });
        registry.Store<ComponentB>().Add(id, new ComponentB { Y = 2f });
        registry.Store<ComponentC>().Add(id, new ComponentC { Z = "three" });

        Assert.Equal(1, registry.Store<ComponentA>().Get(id).X);
        Assert.Equal(2f, registry.Store<ComponentB>().Get(id).Y);
        Assert.Equal("three", registry.Store<ComponentC>().Get(id).Z);
    }

    [Fact]
    public void TryGetStore_ReturnsNull_BeforeFirstAccess()
    {
        var registry = new ComponentStoreRegistry();

        var store = registry.TryGetStore<ComponentA>();

        Assert.Null(store);
    }

    [Fact]
    public void TryGetStore_ReturnStore_AfterFirstAccess()
    {
        var registry = new ComponentStoreRegistry();
        registry.Store<ComponentA>();  // Trigger creation

        var store = registry.TryGetStore<ComponentA>();

        Assert.NotNull(store);
        Assert.IsType<ComponentStore<ComponentA>>(store);
    }
}
