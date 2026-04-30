using System;
using System.Collections.Generic;
using System.Threading;

namespace APIFramework.Systems.Movement;

/// <summary>
/// Bounded LRU cache for pathfinding queries.
/// Keyed by PathQueryKey (including topologyVersion); value is the computed path.
/// When maxEntries is exceeded, the least-recently-used entry is evicted.
/// Thread-safe: uses Interlocked for stats; engine is single-threaded by design.
/// </summary>
public sealed class PathfindingCache
{
    private readonly int _maxEntries;
    private readonly Dictionary<PathQueryKey, LinkedListNode<KeyValuePair<PathQueryKey, IReadOnlyList<(int X, int Y)>>>> _map;
    private readonly LinkedList<KeyValuePair<PathQueryKey, IReadOnlyList<(int X, int Y)>>> _lru;
    private long _hits;
    private long _misses;

    public PathfindingCache(int maxEntries)
    {
        if (maxEntries <= 0) throw new ArgumentException("maxEntries must be > 0", nameof(maxEntries));
        _maxEntries = maxEntries;
        _map = new Dictionary<PathQueryKey, LinkedListNode<KeyValuePair<PathQueryKey, IReadOnlyList<(int X, int Y)>>>>(maxEntries);
        _lru = new LinkedList<KeyValuePair<PathQueryKey, IReadOnlyList<(int X, int Y)>>>();
        _hits = 0;
        _misses = 0;
    }

    /// <summary>Try to get a cached path. If found, moves the entry to the end (most-recently-used).</summary>
    public bool TryGet(PathQueryKey key, out IReadOnlyList<(int X, int Y)> path)
    {
        if (_map.TryGetValue(key, out var node))
        {
            _lru.Remove(node);
            _lru.AddLast(node);
            path = node.Value.Value;
            Interlocked.Increment(ref _hits);
            return true;
        }
        path = default!;
        Interlocked.Increment(ref _misses);
        return false;
    }

    /// <summary>Put a path into the cache. If the key already exists, updates its value. If full, evicts the least-recently-used entry.</summary>
    public void Put(PathQueryKey key, IReadOnlyList<(int X, int Y)> path)
    {
        if (_map.TryGetValue(key, out var existing))
        {
            _lru.Remove(existing);
            _map.Remove(key);
        }
        else if (_lru.Count >= _maxEntries)
        {
            var lru = _lru.First;
            if (lru != null)
            {
                _map.Remove(lru.Value.Key);
                _lru.RemoveFirst();
            }
        }

        var newEntry = new KeyValuePair<PathQueryKey, IReadOnlyList<(int X, int Y)>>(key, path);
        var newNode = _lru.AddLast(newEntry);
        _map[key] = newNode;
    }

    /// <summary>Clear all cached entries.</summary>
    public void Clear()
    {
        _map.Clear();
        _lru.Clear();
    }

    /// <summary>Number of entries currently in the cache.</summary>
    public int Count => _map.Count;

    /// <summary>Total cache hits since instantiation.</summary>
    public long Hits => Interlocked.Read(ref _hits);

    /// <summary>Total cache misses since instantiation.</summary>
    public long Misses => Interlocked.Read(ref _misses);

    /// <summary>Cache hit rate (or 0 if no queries have been made).</summary>
    public double HitRate
    {
        get
        {
            long total = Hits + Misses;
            return total == 0 ? 0.0 : (double)Hits / total;
        }
    }
}
