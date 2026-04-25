namespace APIFramework.Components;

/// <summary>
/// Interior light-source type. Ten values mirror Warden.Contracts.Telemetry.LightKind
/// field-for-field so the projector can map without translation.
/// </summary>
public enum LightKind
{
    OverheadFluorescent = 0,
    DeskLamp            = 1,
    ServerLed           = 2,
    BreakroomStrip      = 3,
    ConferenceTrack     = 4,
    ExteriorWall        = 5,
    SignageGlow         = 6,
    Neon                = 7,
    MonitorGlow         = 8,
    OtherInterior       = 9,
}
