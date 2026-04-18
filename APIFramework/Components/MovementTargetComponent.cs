using System;

namespace APIFramework.Components;

/// <summary>
/// Present while the entity is navigating toward a world object.
/// Set by action systems when the target is out of range; removed by MovementSystem on arrival.
/// </summary>
public struct MovementTargetComponent
{
    /// <summary>Id of the world-object entity to move toward.</summary>
    public Guid   TargetEntityId;

    /// <summary>Human-readable destination name shown in UI and debug ("Fridge", "Bed", etc.).</summary>
    public string? Label;
}
