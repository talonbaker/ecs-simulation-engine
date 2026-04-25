using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems.Movement;

/// <summary>
/// Singleton A* pathfinding service for the tile grid.
/// Computes paths that avoid ObstacleTag entities and prefer doorway tiles.
/// Seeded tie-break noise ensures two NPCs taking the same trip with different
/// seeds trace slightly different routes (the "natural paths" quality goal).
/// </summary>
public sealed class PathfindingService
{
    private readonly EntityManager _em;
    private readonly int           _worldWidth;
    private readonly int           _worldHeight;
    private readonly float         _doorwayDiscount;
    private readonly float         _tieBreakNoiseScale;

    private static readonly (int dx, int dy)[] Directions = { (0, -1), (0, 1), (-1, 0), (1, 0) };

    public PathfindingService(EntityManager em, int worldWidth, int worldHeight, MovementConfig cfg)
    {
        _em                 = em;
        _worldWidth         = worldWidth;
        _worldHeight        = worldHeight;
        _doorwayDiscount    = cfg.Pathfinding.DoorwayDiscount;
        _tieBreakNoiseScale = cfg.Pathfinding.TieBreakNoiseScale;
    }

    /// <summary>
    /// Returns the tile waypoints from start (exclusive) to goal (inclusive).
    /// Returns an empty list when start == goal or no path exists.
    /// The same (from, to, seed) triple always produces the same path.
    /// </summary>
    public IReadOnlyList<(int X, int Y)> ComputePath(int fromX, int fromY, int toX, int toY, int seed)
    {
        if (fromX == toX && fromY == toY)
            return Array.Empty<(int, int)>();

        var obstacles = BuildObstacleSet();
        var doorways  = BuildDoorwaySet();

        var open    = new MinHeap();
        var gCosts  = new Dictionary<(int, int), float>();
        var parents = new Dictionary<(int, int), (int, int)>();
        var closed  = new HashSet<(int, int)>();

        var start = (fromX, fromY);
        var goal  = (toX, toY);

        gCosts[start] = 0f;
        open.Enqueue(start, Heuristic(fromX, fromY, toX, toY));

        while (open.Count > 0)
        {
            var current = open.Dequeue();

            if (current == goal)
                return ReconstructPath(parents, start, goal);

            if (!closed.Add(current)) continue;

            foreach (var (dx, dy) in Directions)
            {
                int nx = current.Item1 + dx;
                int ny = current.Item2 + dy;

                if (nx < 0 || nx >= _worldWidth || ny < 0 || ny >= _worldHeight) continue;
                var next = (nx, ny);
                if (closed.Contains(next)) continue;
                if (obstacles.Contains(next)) continue;

                float stepCost    = 1.0f - (doorways.Contains(next) ? _doorwayDiscount : 0f);
                float tentativeG  = gCosts[current] + stepCost;

                if (gCosts.TryGetValue(next, out float existingG) && tentativeG >= existingG)
                    continue;

                gCosts[next]  = tentativeG;
                parents[next] = current;

                float h     = Heuristic(nx, ny, toX, toY);
                float noise = TileNoise(nx, ny, seed) * _tieBreakNoiseScale;
                open.Enqueue(next, tentativeG + h + noise);
            }
        }

        return Array.Empty<(int, int)>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static float Heuristic(int x, int y, int tx, int ty) =>
        Math.Abs(x - tx) + Math.Abs(y - ty);

    private static float TileNoise(int x, int y, int seed)
    {
        int hash = (x * 73856093) ^ (y * 19349663) ^ (seed * 83492791);
        return ((hash & 0x7FFFFFFF) % 65536) / 65536f;
    }

    private static IReadOnlyList<(int, int)> ReconstructPath(
        Dictionary<(int, int), (int, int)> parents,
        (int, int) start,
        (int, int) goal)
    {
        var path = new List<(int, int)>();
        var cur  = goal;
        while (cur != start)
        {
            path.Add(cur);
            cur = parents[cur];
        }
        path.Reverse();
        return path;
    }

    private HashSet<(int, int)> BuildObstacleSet()
    {
        var set = new HashSet<(int, int)>();
        foreach (var e in _em.Query<ObstacleTag>())
        {
            if (!e.Has<PositionComponent>()) continue;
            var pos = e.Get<PositionComponent>();
            set.Add(((int)MathF.Round(pos.X), (int)MathF.Round(pos.Z)));
        }
        return set;
    }

    private HashSet<(int, int)> BuildDoorwaySet()
    {
        var rooms = new List<BoundsRect>();
        foreach (var e in _em.Query<RoomTag>())
        {
            if (!e.Has<RoomComponent>()) continue;
            rooms.Add(e.Get<RoomComponent>().Bounds);
        }

        var doorways = new HashSet<(int, int)>();
        if (rooms.Count < 2) return doorways;

        foreach (var room in rooms)
        {
            for (int tx = room.X; tx < room.X + room.Width; tx++)
            {
                for (int ty = room.Y; ty < room.Y + room.Height; ty++)
                {
                    foreach (var other in rooms)
                    {
                        if (other == room) continue;
                        if (IsWithin1TileOf(tx, ty, other))
                        {
                            doorways.Add((tx, ty));
                            break;
                        }
                    }
                }
            }
        }

        return doorways;
    }

    private static bool IsWithin1TileOf(int tx, int ty, BoundsRect rect) =>
        tx >= rect.X - 1 && tx <= rect.X + rect.Width  &&
        ty >= rect.Y - 1 && ty <= rect.Y + rect.Height;

    // ── Binary min-heap (netstandard2.1 compatible) ───────────────────────────

    private sealed class MinHeap
    {
        private readonly List<(float priority, (int x, int y) node)> _data = new();

        public int Count => _data.Count;

        public void Enqueue((int x, int y) node, float priority)
        {
            _data.Add((priority, node));
            SiftUp(_data.Count - 1);
        }

        public (int x, int y) Dequeue()
        {
            var result = _data[0].node;
            int last   = _data.Count - 1;
            _data[0]   = _data[last];
            _data.RemoveAt(last);
            if (_data.Count > 0) SiftDown(0);
            return result;
        }

        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_data[parent].priority <= _data[i].priority) break;
                (_data[parent], _data[i]) = (_data[i], _data[parent]);
                i = parent;
            }
        }

        private void SiftDown(int i)
        {
            int n = _data.Count;
            while (true)
            {
                int best  = i;
                int left  = 2 * i + 1;
                int right = 2 * i + 2;
                if (left  < n && _data[left].priority  < _data[best].priority) best = left;
                if (right < n && _data[right].priority < _data[best].priority) best = right;
                if (best == i) break;
                (_data[best], _data[i]) = (_data[i], _data[best]);
                i = best;
            }
        }
    }
}
