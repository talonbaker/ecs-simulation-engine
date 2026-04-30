using System.Collections.Generic;
using APIFramework.Systems.Movement;
using Xunit;

namespace APIFramework.Tests.Systems.Movement;

public class PathfindingCacheTests
{
    private static IReadOnlyList<(int X, int Y)> MakePath(params (int, int)[] tiles)
        => tiles;

    // AT-03: basic put/get
    [Fact]
    public void TryGet_AfterPut_ReturnsTrueWithStoredPath()
    {
        var cache = new PathfindingCache(16);
        var key   = new PathQueryKey(0, 0, 5, 5, 1, 0L);
        var path  = MakePath((1, 0), (2, 0), (3, 0), (4, 0), (5, 0));

        cache.Put(key, path);

        Assert.True(cache.TryGet(key, out var result));
        Assert.Equal(path, result);
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var cache = new PathfindingCache(16);
        var key   = new PathQueryKey(0, 0, 5, 5, 1, 0L);

        Assert.False(cache.TryGet(key, out _));
    }

    // AT-03: LRU eviction — 5 distinct puts into cache of size 4 evicts the oldest
    [Fact]
    public void Put_ExceedsCapacity_EvictsLeastRecentlyUsed()
    {
        var cache = new PathfindingCache(4);
        var keys  = new PathQueryKey[5];
        for (int i = 0; i < 5; i++)
            keys[i] = new PathQueryKey(i, 0, 9, 9, 0, 0L);

        for (int i = 0; i < 5; i++)
            cache.Put(keys[i], MakePath((i, 0)));

        // keys[0] was LRU — should be evicted
        Assert.False(cache.TryGet(keys[0], out _));

        // keys[1..4] should still be present
        for (int i = 1; i < 5; i++)
            Assert.True(cache.TryGet(keys[i], out _));

        Assert.Equal(4, cache.Count);
    }

    [Fact]
    public void TryGet_PromotesToMru_ProtectsFromEviction()
    {
        var cache = new PathfindingCache(3);
        var k0    = new PathQueryKey(0, 0, 9, 9, 0, 0L);
        var k1    = new PathQueryKey(1, 0, 9, 9, 0, 0L);
        var k2    = new PathQueryKey(2, 0, 9, 9, 0, 0L);

        cache.Put(k0, MakePath((0, 0)));
        cache.Put(k1, MakePath((1, 0)));
        cache.Put(k2, MakePath((2, 0)));

        // Access k0 — moves it to MRU
        cache.TryGet(k0, out _);

        // Add k3 — should evict k1 (now LRU) not k0
        var k3 = new PathQueryKey(3, 0, 9, 9, 0, 0L);
        cache.Put(k3, MakePath((3, 0)));

        Assert.True(cache.TryGet(k0, out _));
        Assert.False(cache.TryGet(k1, out _));  // k1 evicted
        Assert.True(cache.TryGet(k2, out _));
        Assert.True(cache.TryGet(k3, out _));
    }

    [Fact]
    public void Clear_EmptiesCache()
    {
        var cache = new PathfindingCache(16);
        for (int i = 0; i < 5; i++)
            cache.Put(new PathQueryKey(i, 0, 9, 9, 0, 0L), MakePath((i, 0)));

        cache.Clear();

        Assert.Equal(0, cache.Count);
        for (int i = 0; i < 5; i++)
            Assert.False(cache.TryGet(new PathQueryKey(i, 0, 9, 9, 0, 0L), out _));
    }

    [Fact]
    public void Put_DuplicateKey_UpdatesValue()
    {
        var cache = new PathfindingCache(16);
        var key   = new PathQueryKey(0, 0, 5, 5, 1, 0L);

        cache.Put(key, MakePath((1, 0)));
        cache.Put(key, MakePath((2, 0)));

        Assert.True(cache.TryGet(key, out var result));
        Assert.Equal((2, 0), result[0]);
        Assert.Equal(1, cache.Count);
    }
}
