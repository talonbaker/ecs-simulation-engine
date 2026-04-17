namespace APIFramework.Core;

/// <summary>
/// A single simulation entity — a Guid-keyed bag of struct components.
///
/// COMPONENT STORAGE
/// ─────────────────
/// Components are stored as Dictionary&lt;Type, object&gt;, which boxes every struct
/// on the heap. This is a known cost accepted in v0.7.x. The fix (ComponentStore&lt;T&gt;
/// typed arrays) is documented in ARCHITECTURE.md and deferred to v0.8+.
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

    private readonly Dictionary<Type, object>      _components = new();
    private readonly Action<Entity, Type, bool>?   _onChange;

    // ── Constructors ──────────────────────────────────────────────────────────

    /// <summary>Creates a new entity with a fresh Guid.</summary>
    public Entity(Action<Entity, Type, bool>? onChange = null)
    {
        Id        = Guid.NewGuid();
        _onChange = onChange;
    }

    /// <summary>Creates an entity with an existing Guid (e.g. for deserialization).</summary>
    public Entity(Guid existingId, Action<Entity, Type, bool>? onChange = null)
    {
        Id        = existingId;
        _onChange = onChange;
    }

    // ── Component API ─────────────────────────────────────────────────────────

    public void Add<T>(T component) where T : struct
    {
        bool isNew = !_components.ContainsKey(typeof(T));
        _components[typeof(T)] = component;
        if (isNew) _onChange?.Invoke(this, typeof(T), true);
    }

    public T Get<T>() where T : struct
        => (T)_components[typeof(T)];

    public bool Has<T>() where T : struct
        => _components.ContainsKey(typeof(T));

    public void Remove<T>() where T : struct
    {
        if (_components.Remove(typeof(T)))
            _onChange?.Invoke(this, typeof(T), false);
    }

    public IEnumerable<object> GetAll()             => _components.Values;
    public IEnumerable<object> GetAllComponents()   => _components.Values;
}