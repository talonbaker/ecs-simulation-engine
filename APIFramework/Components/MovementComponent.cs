namespace APIFramework.Components;

/// <summary>Movement capability — speed and arrival threshold.</summary>
public struct MovementComponent
{
    /// <summary>World-units per game-second at TimeScale=1.</summary>
    public float Speed;

    /// <summary>Distance at which the entity is considered to have arrived at its target.</summary>
    public float ArrivalDistance;
}
