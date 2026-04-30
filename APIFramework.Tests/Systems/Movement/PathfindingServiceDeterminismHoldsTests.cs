using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Movement;

/// <summary>
/// AT-05: Cache hit and cache miss return identical paths for identical inputs
/// at the same topology version. Confirms tie-break noise is preserved through caching.
/// </summary>
public class PathfindingServiceDeterminismHoldsTests
{
    [Fact]
    public void CacheHit_ReturnsSamePathAsCacheMiss_SameTopology()
    {
        var em    = new EntityManager();
        var bus   = new StructuralChangeBus();
        var cache = new PathfindingCache(512);
        var svc   = new PathfindingService(em, 32, 32, new MovementConfig(), cache, bus);

        // First call: cache miss → computes and stores
        var miss = svc.ComputePath(0, 0, 15, 15, seed: 7);

        // Second call: cache hit → retrieves stored value
        var hit = svc.ComputePath(0, 0, 15, 15, seed: 7);

        Assert.Equal(miss.Count, hit.Count);
        for (int i = 0; i < miss.Count; i++)
            Assert.Equal(miss[i], hit[i]);
    }

    [Fact]
    public void AfterClearAndRecompute_SameInputProducesSamePath()
    {
        // Confirms determinism: clearing the cache and recomputing gives identical results.
        var em    = new EntityManager();
        var bus   = new StructuralChangeBus();
        var cache = new PathfindingCache(512);
        var svc   = new PathfindingService(em, 32, 32, new MovementConfig(), cache, bus);

        var first = svc.ComputePath(3, 4, 12, 8, seed: 99).ToList();

        cache.Clear();

        var second = svc.ComputePath(3, 4, 12, 8, seed: 99).ToList();

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
            Assert.Equal(first[i], second[i]);
    }

    [Fact]
    public void TiebBreakNoise_DifferentSeeds_ProduceDifferentPathsRegardlessOfCache()
    {
        var em    = new EntityManager();
        var bus   = new StructuralChangeBus();
        var cache = new PathfindingCache(512);
        var svc   = new PathfindingService(em, 32, 32, new MovementConfig(), cache, bus);

        var pathA = svc.ComputePath(0, 0, 10, 10, seed: 1);
        var pathB = svc.ComputePath(0, 0, 10, 10, seed: 99999);

        Assert.Equal((10, 10), pathA[^1]);
        Assert.Equal((10, 10), pathB[^1]);

        // At least one intermediate tile should differ (same assertion as original tests)
        bool anyDiff = false;
        for (int i = 0; i < pathA.Count - 1; i++)
        {
            if (pathA[i] != pathB[i]) { anyDiff = true; break; }
        }
        Assert.True(anyDiff);
    }
}
