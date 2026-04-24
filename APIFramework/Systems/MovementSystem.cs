using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Moves entities toward their current navigation target.
///
/// TWO MOVEMENT MODES
/// ──────────────────
/// 1. Directed: entity has a MovementTargetComponent pointing at a world-object
///    entity.  The system resolves that entity's PositionComponent and steers
///    toward it.  On arrival (within ArrivalDistance) MovementTargetComponent is
///    removed so the requesting system can detect arrival and act.
///
/// 2. Wander: no MovementTargetComponent present.  The system picks a random XZ
///    destination within WorldBounds and moves toward it.  On arrival a new random
///    destination is chosen, so entities always drift gently around the scene.
///
/// SPEED UNITS
/// ───────────
/// MovementComponent.Speed is world-units per GAME-second.
/// Systems receive game-scaled deltaTime (realDeltaTime * TimeScale), so with
/// TimeScale=120 and 60fps:  scaledDelta ≈ 1.92 game-s/frame
///   speed = 0.04 world-units/game-s  →  ~0.08 units/frame  →  ~10 units in ~13 s
/// </summary>
public class MovementSystem : ISystem
{
    public float WorldMinX { get; set; } = 1f;
    public float WorldMaxX { get; set; } = 9f;
    public float WorldMinZ { get; set; } = 1f;
    public float WorldMaxZ { get; set; } = 9f;

    // Per-entity wander destinations (Guid → target XZ).
    private readonly Dictionary<Guid, (float X, float Z)> _wanderTargets = new();

    // Seeded PRNG so wandering is deterministic / reproducible.
    // Supplied by SimulationBootstrapper so the full simulation shares one seed chain.
    private readonly SeededRandom _rng;

    /// <summary>
    /// Initialises the system with the simulation's shared <see cref="SeededRandom"/>.
    /// Two runs with the same seed produce identical wander sequences.
    /// </summary>
    public MovementSystem(SeededRandom rng)
    {
        _rng = rng;
    }

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

            var pos  = entity.Get<PositionComponent>();
            var move = entity.Get<MovementComponent>();

            // Resolve destination.
            float targetX, targetZ;
            bool  directed = false;

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
                    // Target disappeared — fall back to wander.
                    entity.Remove<MovementTargetComponent>();
                    (targetX, targetZ) = GetOrPickWander(entity.Id);
                }
            }
            else
            {
                (targetX, targetZ) = GetOrPickWander(entity.Id);
            }

            // Steer toward destination.
            float dx   = targetX - pos.X;
            float dz   = targetZ - pos.Z;
            float dist = MathF.Sqrt(dx * dx + dz * dz);

            if (dist <= move.ArrivalDistance)
            {
                if (directed)
                    entity.Remove<MovementTargetComponent>();

                // Pick a fresh wander target whether directed or wandering.
                _wanderTargets[entity.Id] = PickRandom();
            }
            else
            {
                float step = Math.Min(move.Speed * deltaTime, dist);
                float inv  = 1f / dist;

                // entity.Add overwrites the existing PositionComponent in-place.
                entity.Add(new PositionComponent
                {
                    X = pos.X + dx * inv * step,
                    Y = pos.Y,
                    Z = pos.Z + dz * inv * step
                });

                _wanderTargets[entity.Id] = (targetX, targetZ);
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
