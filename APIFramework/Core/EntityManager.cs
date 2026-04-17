namespace APIFramework.Core;

/// <summary>
/// Owns all Entity instances and maintains a component-type index so that
/// Query&lt;T&gt;() is O(1) instead of the previous O(E) full scan.
///
/// HOW THE INDEX WORKS
/// ────────────────────
/// Each entity is created with an onChange callback that fires when a component
/// type is added to or removed from that entity. EntityManager listens to those
/// callbacks and keeps _componentIndex updated:
///
///   _componentIndex[typeof(T)] = { entity A, entity C, entity F, … }
///
/// Query&lt;T&gt;() then returns the pre-built bucket — no scanning, no LINQ.
///
/// THREAD SAFETY
/// ─────────────
/// Not thread-safe. All entity mutations must happen on the simulation thread.
/// When parallel system execution is added in v0.8+, systems within a phase
/// will be allowed to READ concurrently but must WRITE via a command queue
/// that is flushed at phase boundaries on the main thread.
/// </summary>
public class EntityManager
{
    private readonly List<Entity>                       _entities        = new();
    private readonly Dictionary<Type, HashSet<Entity>> _componentIndex  = new();

    public IReadOnlyList<Entity> Entities => _entities;

    /// <summary>
    /// Number of distinct component types currently tracked in the index.
    /// Useful for diagnostics — shows how many query buckets are live.
    /// </summary>
    public int ComponentTypeCount => _componentIndex.Count;

    // ── Entity lifecycle ──────────────────────────────────────────────────────

    public Entity CreateEntity()
    {
        var entity = new Entity(OnComponentChanged);
        _entities.Add(entity);
        return entity;
    }

    /// <summary>Creates an entity with a pre-existing Guid (e.g. deserialization).</summary>
    public Entity CreateEntity(Guid existingId)
    {
        var entity = new Entity(existingId, OnComponentChanged);
        _entities.Add(entity);
        return entity;
    }

    public void DestroyEntity(Entity entity)
    {
        // Flush entity from every index bucket it occupies before removing it.
        foreach (var bucket in _componentIndex.Values)
            bucket.Remove(entity);

        _entities.Remove(entity);
    }

    // ── Query API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all entities that currently have component T.
    /// O(1) — returns the pre-built bucket from the component index.
    /// Returns an empty enumerable (not null) when no entities have T.
    /// </summary>
    public IEnumerable<Entity> Query<T>() where T : struct
    {
        return _componentIndex.TryGetValue(typeof(T), out var bucket)
            ? bucket
            : [];
    }

    /// <summary>Returns every entity managed by this EntityManager.</summary>
    public IEnumerable<Entity> GetAllEntities() => _entities;

    // ── Index maintenance ─────────────────────────────────────────────────────

    private void OnComponentChanged(Entity entity, Type componentType, bool added)
    {
        if (added)
        {
            if (!_componentIndex.TryGetValue(componentType, out var bucket))
            {
                bucket = new HashSet<Entity>();
                _componentIndex[componentType] = bucket;
            }
            bucket.Add(entity);
        }
        else
        {
            if (_componentIndex.TryGetValue(componentType, out var bucket))
                bucket.Remove(entity);
        }
    }
}