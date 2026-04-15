namespace APIFramework.Core;

public class Entity
{
    public Guid Id { get; } = Guid.NewGuid();
    public string ShortId => Id.ToString().Substring(0, 8).ToUpper();
    private readonly Dictionary<Type, object> _components = new();

    // Constructor creates a fresh Guid automatically
    public Entity()
    {
        Id = Guid.NewGuid();
    }

    // Constructor for loading an existing entity with a known Guid
    public Entity(Guid existingId)
    {
        Id = existingId;
    }

    public void Add<T>(T component) where T : struct
        => _components[typeof(T)] = component;

    public T Get<T>() where T : struct
        => (T)_components[typeof(T)];

    public bool Has<T>() where T : struct
        => _components.ContainsKey(typeof(T));

    public void Remove<T>() where T : struct
        => _components.Remove(typeof(T));

    public IEnumerable<object> GetAll() => _components.Values;

    public IEnumerable<object> GetAllComponents()
    {
        // Return all component instances stored for this entity
        return _components.Values;
    }
}