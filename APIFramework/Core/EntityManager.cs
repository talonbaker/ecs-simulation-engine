namespace APIFramework.Core;

/// <summary>
/// Owns all Entity instances and maintains a component-type index so that
/// Query&lt;T&gt;() is O(1) instead of the previous O(E) full scan.
///
/// HOW THE INDEX WORKS
/// --------------------
/// Each entity is created with an onChange callback that fires when a component
/// type is added to or removed from that entity. EntityManager listens to those
/// callbacks and keeps _componentIndex updated:
///
///   _componentIndex[typeof(T)] = { entity A, entity C, entity F, … }
///
/// Query&lt;T&gt;() then returns the pre-built bucket — no scanning, no LINQ.
///
/// THREAD SAFETY
/// -------------
/// Not thread-safe. All entity mutations must happen on the simulation thread.
/// When parallel system execution is added in v0.8+, systems within a phase
/// will be allowed to READ concurrently but must WRITE via a command queue
/// that is flushed at phase boundaries on the main thread.
/// </summary>
public class EntityManager
{
    private readonly List<Entity>                       _entities        = new();
    private readonly Dictionary<Type, HashSet<Entity>> _componentIndex  = new();

    // -- Deterministic entity ID counter --------------------------------------
    //
    // Using a sequential counter instead of Guid.NewGuid() makes every entity
    // ID reproducible across runs: given the same bootstrapper code path, the
    // same counter value is always assigned to the same logical entity.
    //
    // This is a prerequisite for the deterministic replay guarantee (WP-04):
    //   Guid.NewGuid() -> different addresses every run -> non-deterministic JSONL.
    //   Counter-based  -> same ID every run             -> byte-identical JSONL.
    //
    // The EntityManager instance is per-simulation, so counter resets to 0
    // for each new simulation. Existing callers that pass an explicit Guid via
    // CreateEntity(Guid) are unaffected.
    private long _idCounter = 0;

    /// <summary>
    /// Read-only view of every entity currently owned by this manager,
    /// in insertion (creation) order.
    /// </summary>
    public IReadOnlyList<Entity> Entities => _entities;

    /// <summary>
    /// Fires immediately before an entity is removed from the manager.
    /// Subscribe to clean up external state (e.g. spatial index entries).
    /// </summary>
    public event Action<Entity>? EntityDestroyed;

    /// <summary>
    /// Number of distinct component types currently tracked in the index.
    /// Useful for diagnostics -- shows how many query buckets are live.
    /// </summary>
    public int ComponentTypeCount => _componentIndex.Count;

    // -- Entity lifecycle ------------------------------------------------------

    /// <summary>
    /// Creates a new entity with a deterministic, counter-based ID.
    /// The first entity created by an EntityManager instance always gets the
    /// same ID, making telemetry byte-identical across runs.
    /// </summary>
    public Entity CreateEntity()
    {
        // Build a 16-byte GUID from the counter (little-endian, upper 8 bytes = 0).
        long count = ++_idCounter;
        var bytes  = new byte[16];
        bytes[0]  = (byte)( count        & 0xFF);
        bytes[1]  = (byte)((count >>  8) & 0xFF);
        bytes[2]  = (byte)((count >> 16) & 0xFF);
        bytes[3]  = (byte)((count >> 24) & 0xFF);
        bytes[4]  = (byte)((count >> 32) & 0xFF);
        bytes[5]  = (byte)((count >> 40) & 0xFF);
        bytes[6]  = (byte)((count >> 48) & 0xFF);
        bytes[7]  = (byte)((count >> 56) & 0xFF);

        var entity = new Entity(new Guid(bytes), OnComponentChanged);
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

    /// <summary>
    /// Removes <paramref name="entity"/> from the manager and from every component
    /// index bucket it occupied. The <see cref="EntityDestroyed"/> event fires first,
    /// before any index mutation, so subscribers can still observe the entity's
    /// components while cleaning up external state (e.g. spatial index entries).
    /// </summary>
    /// <param name="entity">The entity to destroy.</param>
    public void DestroyEntity(Entity entity)
    {
        EntityDestroyed?.Invoke(entity);

        // Flush entity from every index bucket it occupies before removing it.
        foreach (var bucket in _componentIndex.Values)
            bucket.Remove(entity);

        _entities.Remove(entity);
    }

    // -- Query API -------------------------------------------------------------

    /// <summary>
    /// Returns all entities that currently have component T.
    /// O(1) -- returns the pre-built bucket from the component index.
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

    // -- Index maintenance -----------------------------------------------------

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
