using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Movement;
using Xunit;

namespace APIFramework.Tests.Systems.Movement;

/// <summary>
/// AT-14 regression: PathfindingService with no bus/cache (old constructor path)
/// remains deterministic. The cache must be invisible to tests that don't opt into it.
/// </summary>
public class RegressionMovementDeterminismTests
{
    private static PathfindingService MakeServiceNoBusNoCache(EntityManager em)
        => new PathfindingService(em, 32, 32, new MovementConfig(), new PathfindingCache(512), new APIFramework.Systems.Spatial.StructuralChangeBus());

    [Fact]
    public void ComputePath_NoBusNoCache_SameSeed_ProducesIdenticalPaths()
    {
        var em  = new EntityManager();
        var svc = MakeServiceNoBusNoCache(em);

        var path1 = svc.ComputePath(0, 0, 15, 15, seed: 42);
        var path2 = svc.ComputePath(0, 0, 15, 15, seed: 42);

        Assert.Equal(path1.Count, path2.Count);
        for (int i = 0; i < path1.Count; i++)
            Assert.Equal(path1[i], path2[i]);
    }

    [Fact]
    public void ComputePath_NoBusNoCache_ObstaclesRespected()
    {
        var em = new EntityManager();

        for (int y = 0; y <= 5; y++)
        {
            var obs = em.CreateEntity();
            obs.Add(new ObstacleTag());
            obs.Add(new PositionComponent { X = 5f, Z = y });
        }

        var svc  = MakeServiceNoBusNoCache(em);
        var path = svc.ComputePath(0, 2, 15, 2, seed: 0);

        Assert.NotEmpty(path);
        Assert.Equal((15, 2), path[^1]);
        foreach (var (x, y) in path)
            Assert.False(x == 5 && y >= 0 && y <= 5, $"Path illegally crosses obstacle at ({x},{y})");
    }
}
