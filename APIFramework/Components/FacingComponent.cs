namespace APIFramework.Components;

/// <summary>
/// Direction an entity is currently looking.
/// Convention: 0 = north, 90 = east, 180 = south, 270 = west (clockwise, same as sun azimuth).
/// Updated each tick by FacingSystem (moving entities) and IdleMovementSystem (stationary entities).
/// </summary>
public struct FacingComponent
{
    /// <summary>Direction in degrees [0, 360). 0 = north, 90 = east.</summary>
    public float DirectionDeg;

    /// <summary>Why the entity is facing this direction.</summary>
    public FacingSource Source;
}
