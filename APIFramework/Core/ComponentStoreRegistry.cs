namespace APIFramework.Core;

/// <summary>
/// Central registry holding one typed store per component type.
///
/// At the first access to Store&lt;T&gt;() for a given type T, a ComponentStore&lt;T&gt;
/// is created and cached. All subsequent accesses return the same instance.
/// This means every component type's store is created exactly once, on-demand.
///
/// The registry itself holds a Dictionary&lt;Type, object&gt; — one boxing per component type,
/// not per entity. When JIT specialisation kicks in, the caller's generic specialization
/// caches the result and the lookup is amortized to near-zero cost.
///
/// For tag-shaped components (zero-field structs), a TagStore&lt;T&gt; specialisation
/// can be used to reduce memory overhead from storing empty values.
/// </summary>
public sealed class ComponentStoreRegistry
{
    private readonly Dictionary<Type, object> _stores = new();

    /// <summary>
    /// Returns the typed store for component type T, creating it if needed.
    /// The returned store is a ComponentStore&lt;T&gt; that holds entity-id-to-T mappings.
    /// </summary>
    public ComponentStore<T> Store<T>() where T : struct
    {
        if (_stores.TryGetValue(typeof(T), out var existing))
            return (ComponentStore<T>)existing;

        var created = new ComponentStore<T>();
        _stores[typeof(T)] = created;
        return created;
    }

    /// <summary>
    /// Returns the typed store for component type T if it has been registered,
    /// or null if Store&lt;T&gt;() has not been called yet.
    /// </summary>
    public ComponentStore<T>? TryGetStore<T>() where T : struct
    {
        if (_stores.TryGetValue(typeof(T), out var existing))
            return (ComponentStore<T>)existing;
        return null;
    }
}
