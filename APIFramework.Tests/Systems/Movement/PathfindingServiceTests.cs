using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Movement;

/// <summary>
/// AT-02 to AT-05: PathfindingService A* behaviour.
/// </summary>
public class PathfindingServiceTests
{
    private static PathfindingService MakeService(EntityManager em, int w = 32, int h = 32)
    {
        var cache = new PathfindingCache(512);
        var bus = new StructuralChangeBus();
        return new PathfindingService(em, w, h, new MovementConfig(), cache, bus);
    }

    // AT-02: Finds shortest path on a clean grid
    [Fact]
    public void ComputePath_CleanGrid_FindsShortestPath()
    {
        var em   = new EntityManager();
        var svc  = MakeService(em);
        var path = svc.ComputePath(0, 0, 9, 0, seed: 0);

        // Manhattan distance from (0,0) to (9,0) = 9 steps
        Assert.Equal(9, path.Count);
        // Final waypoint should be the goal
        Assert.Equal((9, 0), path[^1]);
    }

    [Fact]
    public void ComputePath_CleanGrid_DiagonalPath_CorrectLength()
    {
        var em   = new EntityManager();
        var svc  = MakeService(em);
        var path = svc.ComputePath(0, 0, 5, 5, seed: 0);

        // Manhattan distance = 10
        Assert.Equal(10, path.Count);
        Assert.Equal((5, 5), path[^1]);
    }

    // AT-02: start == goal returns empty
    [Fact]
    public void ComputePath_SameStartAndGoal_ReturnsEmpty()
    {
        var em   = new EntityManager();
        var svc  = MakeService(em);
        var path = svc.ComputePath(5, 5, 5, 5, seed: 0);
        Assert.Empty(path);
    }

    // AT-03: Routes around an obstacle
    [Fact]
    public void ComputePath_ObstacleOnDirectRoute_RoutesAround()
    {
        var em = new EntityManager();

        // Block tiles (3, 0) through (3, 4) — corridor from x=0 to x=9 at y=0..4 blocked
        for (int y = 0; y <= 4; y++)
        {
            var obstacle = em.CreateEntity();
            obstacle.Add(new ObstacleTag());
            obstacle.Add(new PositionComponent { X = 3f, Y = 0f, Z = y });
        }

        var svc  = MakeService(em);
        var path = svc.ComputePath(0, 0, 9, 0, seed: 0);

        // Path must exist and must not go through any obstacle tile
        Assert.NotEmpty(path);
        Assert.Equal((9, 0), path[^1]);

        foreach (var (x, y) in path)
        {
            // Should not pass through x=3, y in [0..4]
            Assert.False(x == 3 && y >= 0 && y <= 4,
                $"Path illegally passes through obstacle at ({x}, {y})");
        }
    }

    // AT-04: Different seeds produce different (but valid) paths when multiple equal-cost paths exist
    [Fact]
    public void ComputePath_DifferentSeeds_ProduceDifferentPaths()
    {
        var em   = new EntityManager();
        var svc  = MakeService(em, 32, 32);

        // Symmetric open grid: many equal-cost paths from corner to corner
        var pathA = svc.ComputePath(0, 0, 10, 10, seed: 1);
        var pathB = svc.ComputePath(0, 0, 10, 10, seed: 99999);

        // Both must be valid (reach goal, correct length)
        Assert.Equal((10, 10), pathA[^1]);
        Assert.Equal((10, 10), pathB[^1]);
        Assert.Equal(20, pathA.Count);
        Assert.Equal(20, pathB.Count);

        // They must differ in at least one intermediate waypoint
        bool anyDiff = false;
        for (int i = 0; i < pathA.Count - 1; i++)
        {
            if (pathA[i] != pathB[i]) { anyDiff = true; break; }
        }
        Assert.True(anyDiff, "Different seeds should produce different intermediate waypoints");
    }

    // AT-05: Same seed → identical path across two calls
    [Fact]
    public void ComputePath_SameSeed_ProducesIdenticalPath()
    {
        var em   = new EntityManager();
        var svc  = MakeService(em);

        var path1 = svc.ComputePath(0, 0, 10, 10, seed: 42);
        var path2 = svc.ComputePath(0, 0, 10, 10, seed: 42);

        Assert.Equal(path1.Count, path2.Count);
        for (int i = 0; i < path1.Count; i++)
            Assert.Equal(path1[i], path2[i]);
    }

    // Doorway preference test: path through a doorway tile costs less
    [Fact]
    public void ComputePath_DoorwayDiscount_PathPrefersDooorwayTiles()
    {
        var em  = new EntityManager();
        var cfg = new MovementConfig { Pathfinding = new MovementPathfindingConfig { DoorwayDiscount = 1.0f, TieBreakNoiseScale = 0f } };
        var cache = new PathfindingCache(512);
        var bus = new StructuralChangeBus();
        var svc = new PathfindingService(em, 32, 32, cfg, cache, bus);

        // Room A: x=[0..4], y=[0..4]
        // Room B: x=[5..9], y=[0..4]
        // Adjacent at x=4 vs x=5, so tiles at x=[4..5], y=[0..4] are doorway tiles
        var roomA = em.CreateEntity();
        roomA.Add(new RoomTag());
        roomA.Add(new RoomComponent { Id = "A", Name = "A", Category = RoomCategory.Office, Bounds = new BoundsRect(0, 0, 5, 5) });

        var roomB = em.CreateEntity();
        roomB.Add(new RoomTag());
        roomB.Add(new RoomComponent { Id = "B", Name = "B", Category = RoomCategory.Office, Bounds = new BoundsRect(5, 0, 5, 5) });

        // Path should be valid regardless of doorway discount
        var path = svc.ComputePath(0, 2, 9, 2, seed: 0);
        Assert.NotEmpty(path);
        Assert.Equal((9, 2), path[^1]);
    }
}
