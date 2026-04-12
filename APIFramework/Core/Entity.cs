namespace APIFramework.Core;

public class Entity
{
    public Guid Id { get; } = Guid.NewGuid();
    private readonly Dictionary<Type, object> _components = new();

    public void Add<T>(T component) where T : struct
        => _components[typeof(T)] = component;

    public T Get<T>() where T : struct
        => (T)_components[typeof(T)];

    public bool Has<T>() where T : struct
        => _components.ContainsKey(typeof(T));

    public void Remove<T>() where T : struct
        => _components.Remove(typeof(T));

    public IEnumerable<object> GetAll() => _components.Values;
}