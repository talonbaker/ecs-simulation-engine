using System;

namespace APIFramework.Components;

/// <summary>Absolute world-space position.  Carried by every living entity and all world objects.</summary>
public struct PositionComponent
{
    public float X;
    public float Y;
    public float Z;

    public readonly float DistanceTo(PositionComponent other)
    {
        float dx = X - other.X, dy = Y - other.Y, dz = Z - other.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public override readonly string ToString() => $"({X:F1}, {Y:F1}, {Z:F1})";
}
