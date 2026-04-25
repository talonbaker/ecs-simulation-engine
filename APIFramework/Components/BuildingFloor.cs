namespace APIFramework.Components;

/// <summary>
/// Building floor. Integer values are intentionally identical to
/// <c>Warden.Contracts.Telemetry.BuildingFloor</c> so projectors can cast without a lookup table.
/// </summary>
public enum BuildingFloor
{
    Basement = 0,
    First    = 1,
    Top      = 2,
    Exterior = 3,
}
