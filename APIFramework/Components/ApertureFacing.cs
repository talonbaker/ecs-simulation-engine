namespace APIFramework.Components;

/// <summary>
/// Cardinal direction a window faces outward. Values mirror Warden.Contracts.Telemetry.ApertureFacing.
/// A "North" aperture is on the north wall; it admits sun when sun azimuth is within ±90° of 0° (north).
/// </summary>
public enum ApertureFacing
{
    /// <summary>Aperture mounted on the north wall.</summary>
    North   = 0,
    /// <summary>Aperture mounted on the east wall.</summary>
    East    = 1,
    /// <summary>Aperture mounted on the south wall.</summary>
    South   = 2,
    /// <summary>Aperture mounted on the west wall.</summary>
    West    = 3,
    /// <summary>Aperture mounted overhead (skylight).</summary>
    Ceiling = 4,
}
