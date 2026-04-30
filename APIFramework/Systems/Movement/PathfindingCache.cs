using System.Collections.Generic;

namespace APIFramework.Systems.Movement;

/// <summary>
/// Bounded LRU cache keyed by the full path query tuple including topology version.
/// Single-threaded — not thread-safe by design (SRD §4.2).
/// </summary>
public sealed class PathfindingCache
{
    private readonly int _maxEntries;
    private readonly Dictionary<PathQueryKey, LinkedListNode<KeyValuePair<PathQueryKey, IReadOnlyList<(int X, int Y)>>>> _map;
    private readonly LinkedList<KeyValuePair<PathQueryKey, IReadOnlyList<(int X, int Y)>>> _lru;

    public int Count => _map.Count;

    public PathfindingCache(int maxEntries)
    {
        _maxEntries = maxEntries;
        _map = new Dictionary<PathQueryKey, LinkedListNode<KeyValuePair<PathQueryKey, IReadOnlyList<(int X, int Y)>>>>(maxEntries + 1);
        _lru = new LinkedList<KeyValuePair<PathQueryKey, IReadOnlyList<(int X, int Y)>>>();
    }

    public bool TryGet(PathQueryKey key, out IReadOnlyList<(int X, int Y)> path)
    {
        if (!_map.TryGetValue(key, out var node))
        {
            path = null!;
            return false;
        }
        // Move to front (most recently used)
        _lru.Remove(node);
        _lru.AddFirst(node);
        path = node.Value.Value;
        return true;
    }

    public void Put(PathQueryKey key, IReadOnlyList<(int X, int Y)> path)
    {
        if (_map.TryGetValue(key, out var existing))
        {
            _lru.Remove(existing);
            _map.Remove(key);
        }

        var pair = new KeyValuePair<PathQueryKey, IReadOnlyList<(int X, int Y)>>(key, path);
        var node = _lru.AddFirst(pair);
        _map[key] = node;

        if (_map.Count > _maxEntries)
        {
            var lruNode = _lru.Last!;
            _lru.RemoveLast();
            _map.Remove(lruNode.Value.Key);
        }
    }

    public void Clear()
    {
        _map.Clear();
        _lru.Clear();
    }
}
