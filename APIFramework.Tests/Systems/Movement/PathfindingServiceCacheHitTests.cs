using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Movement;

/// <summary>
/// AT-04: Identical (from, to, seed) against unchanged topology hits cache.
/// Instruments the cache with Count checks to confirm hit vs miss.
/// </summary>
public class PathfindingServiceCacheHitTests
{
    private static (PathfindingService svc, PathfindingCache cache) MakeService(EntityManager em, int w = 32, int h = 32)
    {
        var bus   = new StructuralChangeBus();
        var cache = new PathfindingCache(512);
        var svc   = new PathfindingService(em, w, h, new MovementConfig(), bus, cache);
        return (svc, cache);
    }

    [Fact]
    public void SecondCall_SameInputsUnchangedTopology_HitsCache()
    {
        var em = new EntityManager();
        var (svc, cache) = MakeService(em);

        Assert.Equal(0, cache.Count);

        var path1 = svc.ComputePath(0, 0, 9, 9, seed: 42);
        Assert.Equal(1, cache.Count);  // first call populates cache

        var path2 = svc.ComputePath(0, 0, 9, 9, seed: 42);
        Assert.Equal(1, cache.Count);  // second call hits — count unchanged

        // Both calls return identical results
        Assert.Equal(path1.Count, path2.Count);
        for (int i = 0; i < path1.Count; i++)
            Assert.Equal(path1[i], path2[i]);
    }

    [Fact]
    public void DifferentSeed_SameTopology_IsDistinctCacheEntry()
    {
        var em = new EntityManager();
        var (svc, cache) = MakeService(em);

        svc.ComputePath(0, 0, 9, 9, seed: 1);
        Assert.Equal(1, cache.Count);

        svc.ComputePath(0, 0, 9, 9, seed: 2);
        Assert.Equal(2, cache.Count);  // different seed = different key
    }

    [Fact]
    public void SameStartGoal_ReturnsEmpty_NotCached()
    {
        var em = new EntityManager();
        var (svc, cache) = MakeService(em);

        var result = svc.ComputePath(5, 5, 5, 5, seed: 0);
        Assert.Empty(result);
        Assert.Equal(0, cache.Count);  // trivial case not cached
    }
}
