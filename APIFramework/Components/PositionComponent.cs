using System;

namespace APIFramework.Components;

/// <summary>Absolute world-space position.  Carried by every living entity and all world objects.</summary>
public struct PositionComponent
{
    /// <summary>X coordinate in world units (east-west).</summary>
    public float X;
    /// <summary>Y coordinate in world units (vertical / floor height; usually 0).</summary>
    public float Y;
    /// <summary>Z coordinate in world units (north-south).</summary>
    public float Z;

    /// <summary>Returns the Euclidean distance from this position to <paramref name="other"/>.</summary>
    public readonly float DistanceTo(PositionComponent other)
    {
        float dx = X - other.X, dy = Y - other.Y, dz = Z - other.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>Returns a "(X, Y, Z)" debug string with one decimal of precision.</summary>
    public override readonly string ToString() => $"({X:F1}, {Y:F1}, {Z:F1})";
}
