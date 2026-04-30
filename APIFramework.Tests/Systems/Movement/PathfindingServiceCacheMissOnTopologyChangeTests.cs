using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Movement;

/// <summary>
/// AT-06 / AT-11: Mutation through IWorldMutationApi invalidates cache;
/// subsequent identical query is a miss; result may differ after topology change.
/// </summary>
public class PathfindingServiceCacheMissOnTopologyChangeTests
{
    [Fact]
    public void AfterMutation_CacheIsCleared_NextCallIsMiss()
    {
        var em    = new EntityManager();
        var bus   = new StructuralChangeBus();
        var cache = new PathfindingCache(512);
        var svc   = new PathfindingService(em, 32, 32, new MovementConfig(), bus, cache);
        var api   = new WorldMutationApi(em, bus);

        // Wire invalidation subscriber
        bus.Subscribe(_ => cache.Clear());

        // First query populates cache
        svc.ComputePath(0, 0, 9, 9, seed: 1);
        Assert.Equal(1, cache.Count);

        // Mutate topology: spawn a structural entity
        api.SpawnStructural(5, 5);

        // Cache must be empty after mutation
        Assert.Equal(0, cache.Count);

        // Re-query is a cache miss (populates again)
        svc.ComputePath(0, 0, 9, 9, seed: 1);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void AfterObstacleAdded_PathAroundObstacleIsComputed()
    {
        var em    = new EntityManager();
        var bus   = new StructuralChangeBus();
        var cache = new PathfindingCache(512);
        var svc   = new PathfindingService(em, 32, 32, new MovementConfig(), bus, cache);
        var api   = new WorldMutationApi(em, bus);

        bus.Subscribe(_ => cache.Clear());

        // Path before obstacle
        var before = svc.ComputePath(0, 5, 9, 5, seed: 0);
        Assert.NotEmpty(before);

        // Spawn a wall of obstacles blocking the direct route at x=4, y=0..9
        for (int y = 0; y <= 9; y++)
        {
            var newId = api.SpawnStructural(4, y);
        }

        // Path after obstacle — must exist and avoid the blocked tiles (x=4, y 0..9)
        var after = svc.ComputePath(0, 5, 9, 5, seed: 0);
        Assert.NotEmpty(after);
        foreach (var (x, y) in after)
            Assert.False(x == 4 && y >= 0 && y <= 9, $"Path illegally crosses obstacle at ({x},{y})");
    }
}
