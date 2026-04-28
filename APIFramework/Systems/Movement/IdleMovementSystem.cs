using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems.Movement;

/// <summary>
/// Per-tick: applies idle micro-movement to stationary NPCs (no active MovementTargetComponent).
/// Two behaviours:
///   1. Position jitter — a small random XZ offset up to SimConfig.movement.idleJitterTiles.
///   2. Posture shift   — with probability idlePostureShiftProb, rotate facing ±90° from
///      current direction, simulating looking around.
/// Uses SeededRandom for determinism.
/// </summary>
public sealed class IdleMovementSystem : ISystem
{
    private readonly SeededRandom  _rng;
    private readonly float         _jitterTiles;
    private readonly float         _postureShiftProb;

    public IdleMovementSystem(SeededRandom rng, MovementConfig cfg)
    {
        _rng              = rng;
        _jitterTiles      = cfg.IdleJitterTiles;
        _postureShiftProb = cfg.IdlePostureShiftProb;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<PositionComponent>())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
            if (!entity.Has<MovementComponent>()) continue;
            if (entity.Has<MovementTargetComponent>()) continue; // goal-directed — not idle

            // Position jitter
            var pos   = entity.Get<PositionComponent>();
            float dx  = (_rng.NextFloat() - 0.5f) * 2f * _jitterTiles;
            float dz  = (_rng.NextFloat() - 0.5f) * 2f * _jitterTiles;
            entity.Add(new PositionComponent { X = pos.X + dx, Y = pos.Y, Z = pos.Z + dz });

            // Posture shift (looking around)
            if (entity.Has<FacingComponent>() && _rng.NextFloat() < _postureShiftProb)
            {
                var facing = entity.Get<FacingComponent>();
                float delta = (_rng.NextFloat() - 0.5f) * 180f; // ±90° range
                float newDir = (facing.DirectionDeg + delta + 360f) % 360f;
                entity.Add(new FacingComponent { DirectionDeg = newDir, Source = FacingSource.Idle });
            }
        }
    }
}
