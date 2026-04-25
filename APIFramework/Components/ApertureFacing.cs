namespace APIFramework.Components;

/// <summary>
/// Cardinal direction a window faces outward. Values mirror Warden.Contracts.Telemetry.ApertureFacing.
/// A "North" aperture is on the north wall; it admits sun when sun azimuth is within ±90° of 0° (north).
/// </summary>
public enum ApertureFacing
{
    North   = 0,
    East    = 1,
    South   = 2,
    West    = 3,
    Ceiling = 4,
}
