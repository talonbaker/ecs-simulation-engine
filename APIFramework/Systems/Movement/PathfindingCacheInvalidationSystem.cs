using APIFramework.Core;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems.Movement;

/// <summary>
/// System that listens to StructuralChangeBus and clears the pathfinding cache
/// whenever topology changes.
///
/// v0.1 implementation: clears the entire cache on any structural change.
/// Future: region-scoped eviction (drop only entries whose bounding box overlaps the change).
/// </summary>
public sealed class PathfindingCacheInvalidationSystem : ISystem
{
    private readonly PathfindingCache _cache;

    public PathfindingCacheInvalidationSystem(StructuralChangeBus bus, PathfindingCache cache)
    {
        _cache = cache;
        bus.Subscribe(_ => _cache.Clear());
    }

    public void Update(EntityManager em, float deltaTime)
    {
        // Nothing to do per-tick; work happens via bus subscription.
    }
}
