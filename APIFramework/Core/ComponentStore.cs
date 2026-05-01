namespace APIFramework.Core;

/// <summary>
/// Per-component-type typed store. Holds component values directly without boxing.
///
/// For a component type T, one ComponentStore&lt;T&gt; instance is created and registered
/// in the ComponentStoreRegistry. The store's Dictionary&lt;Guid, T&gt; holds entity-id-to-value
/// mappings in their original value types — no boxing occurs for value-type T.
///
/// Entity.Get&lt;T&gt;() delegates to store.Get(Id), which returns T directly.
/// Entity.Set&lt;T&gt;(value) delegates to store.Set(Id, value), copying the struct into the dictionary.
///
/// The only boxing in the engine after this refactor happens once per component type
/// at the registry level (when Store&lt;T&gt;() caches the store instance), not per entity
/// per component access.
/// </summary>
public sealed class ComponentStore<T> where T : struct
{
    private readonly Dictionary<Guid, T> _data = new();

    /// <summary>Retrieves component T for the given entity ID. Throws KeyNotFoundException if absent.</summary>
    public T Get(Guid entityId)
    {
        if (!_data.TryGetValue(entityId, out var value))
            throw new KeyNotFoundException($"Entity {entityId} has no {typeof(T).Name}");
        return value;
    }

    /// <summary>Attempts to retrieve component T for the given entity ID.</summary>
    public bool TryGet(Guid entityId, out T value) => _data.TryGetValue(entityId, out value);

    /// <summary>Returns true if the entity has component T attached.</summary>
    public bool Has(Guid entityId) => _data.ContainsKey(entityId);

    /// <summary>Stores component T for the given entity ID. Overwrites if already present.</summary>
    public void Set(Guid entityId, T value) => _data[entityId] = value;

    /// <summary>Adds component T for the given entity ID. Throws if already present.</summary>
    public void Add(Guid entityId, T value)
    {
        if (_data.ContainsKey(entityId))
            throw new InvalidOperationException($"Entity {entityId} already has {typeof(T).Name}");
        _data[entityId] = value;
    }

    /// <summary>Removes component T from the given entity ID. Does nothing if absent (idempotent).</summary>
    public void Remove(Guid entityId) => _data.Remove(entityId);

    /// <summary>Enumerates all entity IDs that have this component.</summary>
    public IEnumerable<Guid> EntityIds => _data.Keys;

    /// <summary>Number of entities with this component.</summary>
    public int Count => _data.Count;
}
