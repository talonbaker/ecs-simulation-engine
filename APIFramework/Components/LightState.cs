namespace APIFramework.Components;

/// <summary>
/// Operational state of a light source. Values mirror Warden.Contracts.Telemetry.LightState.
/// </summary>
public enum LightState
{
    On        = 0,
    Off       = 1,
    Flickering = 2,
    Dying     = 3,
}
