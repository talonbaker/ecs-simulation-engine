using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Moves entities toward their current navigation target.
///
/// THREE MOVEMENT MODES
/// --------------------
/// 1. Path-following: entity has a PathComponent with cached A* waypoints.
///    Advances along waypoints one at a time; removes PathComponent once the
///    final waypoint is reached, then falls through to directed / wander mode.
///
/// 2. Directed: entity has a MovementTargetComponent pointing at a world-object
///    entity. Steers directly toward it; removes the component on arrival.
///
/// 3. Wander: no active target or path. Picks a random XZ destination and drifts.
///
/// SPEED
/// -----
/// Effective speed = Speed × SpeedModifier × deltaTime.
/// SpeedModifier defaults 1.0; written each tick by MovementSpeedModifierSystem.
///
/// VELOCITY RECORDING
/// ------------------
/// LastVelocityX/Z on MovementComponent receive the actual XZ displacement this
/// tick. FacingSystem reads them to derive facing direction.
/// </summary>
/// <remarks>
/// Reads: <see cref="PositionComponent"/>, <see cref="MovementComponent"/>,
/// <see cref="MovementTargetComponent"/>, <see cref="PathComponent"/>,
/// <see cref="LifeStateComponent"/>, world-object positions
/// (<see cref="FridgeComponent"/>, <see cref="SinkComponent"/>,
/// <see cref="ToiletComponent"/>, <see cref="BedComponent"/>).<br/>
/// Writes: <see cref="PositionComponent"/> (single writer of agent position),
/// <see cref="PathComponent"/> waypoint index, <see cref="MovementComponent"/>
/// LastVelocityX/Z; removes <see cref="MovementTargetComponent"/> on arrival.<br/>
/// Phase: World — runs in registration order alongside the rest of the movement
/// quality pipeline.
/// </remarks>
public class MovementSystem : ISystem
{
    /// <summary>Lower X bound for wander destinations.</summary>
    public float WorldMinX { get; set; } = 1f;
    /// <summary>Upper X bound for wander destinations.</summary>
    public float WorldMaxX { get; set; } = 9f;
    /// <summary>Lower Z bound for wander destinations.</summary>
    public float WorldMinZ { get; set; } = 1f;
    /// <summary>Upper Z bound for wander destinations.</summary>
    public float WorldMaxZ { get; set; } = 9f;

    private readonly Dictionary<Guid, (float X, float Z)> _wanderTargets = new();
    private readonly SeededRandom                          _rng;

    /// <summary>Constructs the movement system with the deterministic RNG used to pick wander destinations.</summary>
    /// <param name="rng">Seeded RNG shared across the simulation.</param>
    public MovementSystem(SeededRandom rng) { _rng = rng; }

    /// <summary>Per-tick steering pass; advances every Alive entity toward its current target/waypoint/wander point.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        // Snapshot world-object positions once per tick.
        var worldPositions = new Dictionary<Guid, PositionComponent>();
        foreach (var e in em.GetAllEntities())
        {
            if ((e.Has<FridgeComponent>()  || e.Has<SinkComponent>() ||
                 e.Has<ToiletComponent>()  || e.Has<BedComponent>())
                && e.Has<PositionComponent>())
            {
                worldPositions[e.Id] = e.Get<PositionComponent>();
            }
        }

        foreach (var entity in em.Query<PositionComponent>())
        {
            if (!entity.Has<MovementComponent>()) continue;
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: deceased position frozen

            var pos  = entity.Get<PositionComponent>();
            var move = entity.Get<MovementComponent>();

            float targetX = 0f;
            float targetZ = 0f;
            bool  directed = false;

            // -- Mode 1: Path-following -----------------------------------------
            bool followingPath = false;
            if (entity.Has<PathComponent>())
            {
                var path = entity.Get<PathComponent>();

                // Skip waypoints that are already within arrival distance.
                while (path.Waypoints != null && path.CurrentWaypointIndex < path.Waypoints.Count)
                {
                    var wp  = path.Waypoints[path.CurrentWaypointIndex];
                    float wdx = wp.X - pos.X;
                    float wdz = wp.Y - pos.Z;
                    if (MathF.Sqrt(wdx * wdx + wdz * wdz) <= move.ArrivalDistance)
                        path.CurrentWaypointIndex++;
                    else
                        break;
                }

                if (path.Waypoints != null && path.CurrentWaypointIndex < path.Waypoints.Count)
                {
                    entity.Add(path); // persist updated index
                    var wp   = path.Waypoints[path.CurrentWaypointIndex];
                    targetX  = wp.X;
                    targetZ  = wp.Y;
                    directed = true;
                    followingPath = true;
                }
                else
                {
                    entity.Remove<PathComponent>();
                }
            }

            // -- Mode 2: Directed toward target entity --------------------------
            if (!followingPath)
            {
                if (entity.Has<MovementTargetComponent>())
                {
                    var mt = entity.Get<MovementTargetComponent>();
                    if (worldPositions.TryGetValue(mt.TargetEntityId, out var wp))
                    {
                        targetX  = wp.X;
                        targetZ  = wp.Z;
                        directed = true;
                    }
                    else
                    {
                        entity.Remove<MovementTargetComponent>();
                        (targetX, targetZ) = GetOrPickWander(entity.Id);
                    }
                }
                else
                {
                    // -- Mode 3: Wander -----------------------------------------
                    (targetX, targetZ) = GetOrPickWander(entity.Id);
                }
            }

            // -- Steer toward resolved destination -----------------------------
            float dx   = targetX - pos.X;
            float dz   = targetZ - pos.Z;
            float dist = MathF.Sqrt(dx * dx + dz * dz);

            float speedMod       = move.SpeedModifier > 0f ? move.SpeedModifier : 1.0f;
            float effectiveSpeed = move.Speed * speedMod;

            if (dist <= move.ArrivalDistance)
            {
                if (directed) entity.Remove<MovementTargetComponent>();

                _wanderTargets[entity.Id] = PickRandom();

                move.LastVelocityX = 0f;
                move.LastVelocityZ = 0f;
                entity.Add(move);
            }
            else
            {
                float step = Math.Min(effectiveSpeed * deltaTime, dist);
                float inv  = 1f / dist;

                float newX = pos.X + dx * inv * step;
                float newZ = pos.Z + dz * inv * step;

                entity.Add(new PositionComponent { X = newX, Y = pos.Y, Z = newZ });
                _wanderTargets[entity.Id] = (targetX, targetZ);

                move.LastVelocityX = newX - pos.X;
                move.LastVelocityZ = newZ - pos.Z;
                entity.Add(move);
            }
        }
    }

    private (float X, float Z) GetOrPickWander(Guid id)
    {
        if (!_wanderTargets.TryGetValue(id, out var wt))
            wt = PickRandom();
        return wt;
    }

    private (float X, float Z) PickRandom()
    {
        float x = _rng.NextFloatRange(WorldMinX, WorldMaxX);
        float z = _rng.NextFloatRange(WorldMinZ, WorldMaxZ);
        return (x, z);
    }
}
