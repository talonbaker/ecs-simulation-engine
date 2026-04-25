using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems.Movement;

/// <summary>
/// Per-tick: detects MovementTargetComponent changes, requests a new path from
/// PathfindingService, and writes a PathComponent to the NPC.
/// Also removes stale PathComponents when MovementTargetComponent is absent.
/// </summary>
public sealed class PathfindingTriggerSystem : ISystem
{
    private readonly PathfindingService                _pathfinder;
    private readonly Dictionary<Entity, Guid>          _lastTargets = new();

    public PathfindingTriggerSystem(PathfindingService pathfinder)
    {
        _pathfinder = pathfinder;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        // --- Build a position cache (only when at least one path needs recomputing) ---
        Dictionary<Guid, (int, int)>? posCache = null;

        foreach (var entity in em.Query<MovementTargetComponent>())
        {
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
