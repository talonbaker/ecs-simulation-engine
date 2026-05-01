namespace APIFramework.Components;

/// <summary>Axis-aligned rectangle in tile units. Used for room bounds.</summary>
public readonly record struct BoundsRect(int X, int Y, int Width, int Height)
{
    /// <summary>Returns true when the tile point is inside this rect (inclusive min, exclusive max).</summary>
    public bool Contains(int tileX, int tileY) =>
        tileX >= X && tileX < X + Width &&
        tileY >= Y && tileY < Y + Height;

    /// <summary>Total tile-area covered by the rect (Width × Height).</summary>
    public int Area => Width * Height;
}
