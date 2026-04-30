namespace APIFramework.Components;

/// <summary>
/// Operational state of a light source. Values mirror Warden.Contracts.Telemetry.LightState.
/// </summary>
public enum LightState
{
    /// <summary>Fixture is operating at nominal intensity.</summary>
    On        = 0,
    /// <summary>Fixture is intentionally off.</summary>
    Off       = 1,
    /// <summary>Fixture is flickering — intensity unchanged but visually unstable.</summary>
    Flickering = 2,
    /// <summary>Fixture is failing — LightSourceStateSystem decrements intensity over time.</summary>
    Dying     = 3,
}
