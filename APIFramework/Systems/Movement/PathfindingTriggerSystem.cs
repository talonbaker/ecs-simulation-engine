using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems.Movement;

/// <summary>
/// Per-tick: detects MovementTargetComponent changes, requests a new path from
/// PathfindingService, and writes a PathComponent to the NPC.
/// Also removes stale PathComponents when MovementTargetComponent is absent.
/// </summary>
/// <remarks>
/// Phase: World (60), registered FIRST in the movement quality pipeline. Reads
/// <c>MovementTargetComponent</c>, <c>PositionComponent</c>; writes <c>PathComponent</c>.
/// Skips non-Alive NPCs.
/// </remarks>
public sealed class PathfindingTriggerSystem : ISystem
{
    private readonly PathfindingService                _pathfinder;
    private readonly Dictionary<Entity, Guid>          _lastTargets = new();

    /// <summary>
    /// Stores the pathfinder used to compute paths each tick.
    /// </summary>
    /// <param name="pathfinder">Singleton pathfinding service.</param>
    public PathfindingTriggerSystem(PathfindingService pathfinder)
    {
        _pathfinder = pathfinder;
    }

    /// <summary>
    /// Per-tick entry point. For each movement-targeted NPC whose target changed since the
    /// last tick, recomputes the path; for any NPC that lost its target, strips the
    /// <c>PathComponent</c>.
    /// </summary>
    /// <param name="em">Entity manager — queried for movement targets and positioned entities.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        // --- Build a position cache (only when at least one path needs recomputing) ---
        Dictionary<Guid, (int, int)>? posCache = null;

        foreach (var entity in em.Query<MovementTargetComponent>())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
            if (!entity.Has<PositionComponent>()) continue;

            var mt = entity.Get<MovementTargetComponent>();

            if (_lastTargets.TryGetValue(entity, out var last) && last == mt.TargetEntityId)
                continue; // target unchanged — keep existing path

            posCache ??= BuildPositionCache(em);

            if (posCache.TryGetValue(mt.TargetEntityId, out var targetTile))
            {
                var npcPos = entity.Get<PositionComponent>();
                int fromX  = (int)MathF.Round(npcPos.X);
                int fromY  = (int)MathF.Round(npcPos.Z);

                int seed = entity.Id.GetHashCode() ^ mt.TargetEntityId.GetHashCode();
                var waypoints = _pathfinder.ComputePath(fromX, fromY, targetTile.Item1, targetTile.Item2, seed);

                entity.Add(new PathComponent { Waypoints = waypoints, CurrentWaypointIndex = 0 });
            }

            _lastTargets[entity] = mt.TargetEntityId;
        }

        // --- Remove stale PathComponents for entities that lost their target ---
        var toRemove = new List<Entity>();
        foreach (var (entity, _) in _lastTargets)
        {
            if (!entity.Has<MovementTargetComponent>())
            {
                if (entity.Has<PathComponent>())
                    entity.Remove<PathComponent>();
                toRemove.Add(entity);
            }
        }
        foreach (var e in toRemove) _lastTargets.Remove(e);
    }

    private static Dictionary<Guid, (int, int)> BuildPositionCache(EntityManager em)
    {
        var cache = new Dictionary<Guid, (int, int)>();
        foreach (var e in em.Query<PositionComponent>())
        {
            var pos = e.Get<PositionComponent>();
            cache[e.Id] = ((int)MathF.Round(pos.X), (int)MathF.Round(pos.Z));
        }
        return cache;
    }
}
