using APIFramework.Core;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems.Movement;

/// <summary>
/// Subscribes to StructuralChangeBus and clears PathfindingCache on every emission.
/// Registered for lifecycle reasons only — no per-tick work. The actual clear
/// happens as a side-effect of bus emissions during producing systems' ticks.
/// </summary>
public sealed class PathfindingCacheInvalidationSystem : ISystem
{
    private readonly PathfindingCache _cache;

    public PathfindingCacheInvalidationSystem(StructuralChangeBus bus, PathfindingCache cache)
    {
        _cache = cache;
        bus.Subscribe(_ => _cache.Clear());
    }

    public void Update(EntityManager em, float deltaTime) { }
}
