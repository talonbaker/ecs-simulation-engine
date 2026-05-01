using APIFramework.Systems.Movement;
using Xunit;

namespace APIFramework.Tests.Systems.Movement;

/// <summary>
/// AT-03 / AT-12: verifies LRU eviction boundary at cacheMaxEntries = 4.
/// </summary>
public class PathfindingCacheLruEvictionTests
{
    [Fact]
    public void At4Entries_FifthPut_EvictsOldest_ThenRequeryCausesNewMiss()
    {
        var cache = new PathfindingCache(maxEntries: 4);

        var keys = new PathQueryKey[5];
        for (int i = 0; i < 5; i++)
            keys[i] = new PathQueryKey(i, 0, 9, 9, 0, 0L);

        for (int i = 0; i < 5; i++)
            cache.Put(keys[i], new[] { (i, i) });

        // Count is bounded at 4
        Assert.Equal(4, cache.Count);

        // Oldest (keys[0]) was evicted
        Assert.False(cache.TryGet(keys[0], out _), "keys[0] should have been evicted");

        // Re-querying the evicted key is a miss
        Assert.False(cache.TryGet(keys[0], out _));

        // Remaining 4 entries are still present
        for (int i = 1; i < 5; i++)
            Assert.True(cache.TryGet(keys[i], out _), $"keys[{i}] should still be in cache");
    }
}
