namespace APIFramework.Core;

public class EntityManager
{
    private readonly List<Entity> _entities = new();
    public IReadOnlyList<Entity> Entities => _entities;

    public Entity CreateEntity()
    {
        var entity = new Entity();
        _entities.Add(entity);
        return entity;
    }

    public void DestroyEntity(Entity entity) => _entities.Remove(entity);

    // This is what your systems will use to find "Mouths" or "Stomachs"
    public IEnumerable<Entity> Query<T>() where T : struct
        => _entities.Where(e => e.Has<T>());

    // Inside EntityManager.cs
    public IEnumerable<Entity> GetAllEntities()
    {
        // If you are using a List<Entity> named _entities, just return it directly.
        return _entities;
    }
}