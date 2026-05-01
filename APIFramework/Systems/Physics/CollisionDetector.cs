using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems.Physics;

public enum HitSurface { None, Wall, Floor }

public readonly struct HitResult
{
    public HitSurface Surface { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }

    public static HitResult None => new() { Surface = HitSurface.None };
}

/// <summary>
/// Single-tick hit detection — no continuous solver. Checks floor clamp (Y ≤ 0)
/// and world-boundary walls. Deterministic: no RNG, no floating-point divergence sources.
/// </summary>
public sealed class CollisionDetector
{
    private readonly PhysicsConfig _cfg;
    private readonly int _worldW;
    private readonly int _worldH;

    public CollisionDetector(PhysicsConfig cfg, int worldWidth, int worldHeight)
    {
        _cfg    = cfg;
        _worldW = worldWidth;
        _worldH = worldHeight;
    }

    /// <summary>
    /// Returns the first hit surface encountered moving from <paramref name="from"/> to
    /// <paramref name="to"/> in a single tick. Returns <see cref="HitResult.None"/> if
    /// the path is clear.
    /// </summary>
    public HitResult DetectHit(PositionComponent from, PositionComponent to, Guid entityId)
    {
        // Floor clamp — Y reaches 0 or below
        if (to.Y <= 0f && from.Y > 0f)
            return new HitResult
            {
                Surface = HitSurface.Floor,
                X = to.X,
                Y = 0f,
                Z = to.Z
            };

        // Floor landing when already at floor and moving sideways
        if (from.Y <= 0f && to.Y <= 0f)
            return CheckWalls(to);

        // Wall check on projected XZ position
        return CheckWalls(to);
    }

    private HitResult CheckWalls(PositionComponent pos)
    {
        float margin = _cfg.WallHitClampMargin;
        float maxX   = _worldW - 1f - margin;
        float maxZ   = _worldH - 1f - margin;

        bool hitWall = pos.X < margin || pos.X > maxX || pos.Z < margin || pos.Z > maxZ;
        if (!hitWall) return HitResult.None;

        float clampedX = Math.Clamp(pos.X, margin, maxX);
        float clampedZ = Math.Clamp(pos.Z, margin, maxZ);

        return new HitResult
        {
            Surface = HitSurface.Wall,
            X = clampedX,
            Y = Math.Max(0f, pos.Y),
            Z = clampedZ
        };
    }
}
