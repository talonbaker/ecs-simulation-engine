namespace APIFramework.Components;

/// <summary>Movement capability — speed, arrival threshold, and per-tick modifiers.</summary>
public struct MovementComponent
{
    /// <summary>World-units per game-second at TimeScale=1.</summary>
    public float Speed;

    /// <summary>Distance at which the entity is considered to have arrived at its target.</summary>
    public float ArrivalDistance;

    /// <summary>
    /// Multiplier applied to Speed each tick. Written by MovementSpeedModifierSystem.
    /// 1.0 = normal speed. Range [0.3, 2.0] after clamping. Defaults to 1.0.
    /// </summary>
    public float SpeedModifier;

    /// <summary>X component of the velocity vector applied last tick. Written by MovementSystem. Read by FacingSystem.</summary>
    public float LastVelocityX;

    /// <summary>Z component of the velocity vector applied last tick. Written by MovementSystem. Read by FacingSystem.</summary>
    public float LastVelocityZ;
}
