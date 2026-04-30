namespace APIFramework.Components;

/// <summary>
/// Building floor. Integer values are intentionally identical to
/// <c>Warden.Contracts.Telemetry.BuildingFloor</c> so projectors can cast without a lookup table.
/// </summary>
public enum BuildingFloor
{
    /// <summary>Subterranean floor.</summary>
    Basement = 0,
    /// <summary>Ground/first floor.</summary>
    First    = 1,
    /// <summary>Top floor of the building.</summary>
    Top      = 2,
    /// <summary>Outside the building (e.g. parking lot, sidewalk).</summary>
    Exterior = 3,
}
