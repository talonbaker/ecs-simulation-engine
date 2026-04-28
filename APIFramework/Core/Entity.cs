namespace APIFramework.Core;

/// <summary>
/// A single simulation entity — a Guid-keyed bag of struct components.
///
/// COMPONENT STORAGE
/// ─────────────────
/// Components are stored in a per-type ComponentStoreRegistry without boxing.
/// Entity holds a reference to the registry; Get&lt;T&gt;() delegates to registry.Store&lt;T&gt;().Get(Id).
/// This refactor (WP-3.0.5) eliminates the O(E) boxing cost of Dictionary&lt;Type, object&gt;.
///
/// CHANGE NOTIFICATION
/// ───────────────────
/// Entity fires an optional onChange callback whenever a component type is added
/// or removed. EntityManager uses this to maintain a component index so that
/// Query&lt;T&gt;() is O(1) rather than O(E). The callback signature is:
///
///   (Entity entity, Type componentType, bool added)
///
/// 'added' is true when the component is being set for the first time on this
/// entity; false when Remove&lt;T&gt;() is called. Overwriting an existing component
/// via Add&lt;T&gt;() does NOT fire the callback — the entity's membership in the index
/// bucket doesn't change, only the value does.
/// </summary>
public class Entity
{
    public Guid   Id      { get; }
    public string ShortId => Id.ToString().Substring(0, 8).ToUpper();

    private readonly ComponentStoreRegistry         _registry;
    private readonly Action<Entity, Type, bool>?   _onChange;

    // ── Constructors ──────────────────────────────────────────────────────────

    /// <summary>Creates a new entity with a fresh Guid and optional registry.</summary>
    public Entity(Action<Entity, Type, bool>? onChange = null)
        : this(new ComponentStoreRegistry(), onChange)
    {
    }

    /// <summary>Creates a new entity with a fresh Guid and the provided registry.</summary>
    public Entity(ComponentStoreRegistry registry, Action<Entity, Type, bool>? onChange = null)
    {
        Id        = Guid.NewGuid();
        _registry = registry;
        _onChange = onChange;
    }

    /// <summary>Creates an entity with an existing Guid (e.g. for deserialization).</summary>
    public Entity(Guid existingId, Action<Entity, Type, bool>? onChange = null)
        : this(existingId, new ComponentStoreRegistry(), onChange)
    {
    }

    /// <summary>Creates an entity with an existing Guid and the provided registry.</summary>
    public Entity(Guid existingId, ComponentStoreRegistry registry, Action<Entity, Type, bool>? onChange = null)
    {
        Id        = existingId;
        _registry = registry;
        _onChange = onChange;
    }

    // ── Component API ─────────────────────────────────────────────────────────

    public void Add<T>(T component) where T : struct
    {
        var store = _registry.Store<T>();
        bool isNew = !store.Has(Id);
        if (isNew)
        {
            store.Add(Id, component);
            _onChange?.Invoke(this, typeof(T), true);
        }
        else
        {
            // Overwrite existing component (no callback — membership doesn't change)
            store.Set(Id, component);
        }
    }

    public T Get<T>() where T : struct
        => _registry.Store<T>().Get(Id);

    public bool Has<T>() where T : struct
        => _registry.Store<T>().Has(Id);

    public void Set<T>(T value) where T : struct
        => _registry.Store<T>().Set(Id, value);

    public void Remove<T>() where T : struct
    {
        var store = _registry.Store<T>();
        if (store.Has(Id))
        {
            store.Remove(Id);
            _onChange?.Invoke(this, typeof(T), false);
        }
    }

    public IEnumerable<object> GetAll()             => GetAllComponents();
    public IEnumerable<object> GetAllComponents()   => [];  // Deprecated; not used in practice

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Hash code derived from <see cref="Id"/> (not memory address).
    ///
    /// WHY THIS MATTERS FOR DETERMINISM
    /// ──────────────────────────────────
    /// <see cref="EntityManager"/> stores entities in <c>HashSet&lt;Entity&gt;</c>
    /// buckets. If <c>GetHashCode()</c> returns a memory-address-based value
    /// (the <c>Object</c> default), bucket placement — and therefore
    /// <c>Query&lt;T&gt;()</c> iteration order — varies between process runs.
    /// Systems that iterate the query in different orders on each run produce
    /// different telemetry, breaking the deterministic replay contract.
    ///
    /// By deriving the hash from <see cref="Id"/> (which is itself
    /// deterministic via <see cref="EntityManager"/>'s counter-based GUID),
    /// the bucket layout is identical across runs for the same entity creation
    /// sequence, making system iteration order reproducible.
    ///
    /// CONTRACT NOTE: <see cref="Equals"/> uses reference equality (the correct
    /// semantic: one C# object = one simulation entity). The contract
    ///   "a.Equals(b) ⟹ a.GetHashCode() == b.GetHashCode()"
    /// is satisfied because two references to the same object share the same
    /// <see cref="Id"/>, and therefore the same hash code.
    /// </summary>
    public override int GetHashCode() => Id.GetHashCode();
}