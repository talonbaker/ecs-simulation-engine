namespace APIFramework.Components;

/// <summary>
/// Interior light-source type. Ten values mirror Warden.Contracts.Telemetry.LightKind
/// field-for-field so the projector can map without translation.
/// </summary>
public enum LightKind
{
    /// <summary>Standard ceiling-mounted fluorescent fixture.</summary>
    OverheadFluorescent = 0,
    /// <summary>Personal desk-lamp.</summary>
    DeskLamp            = 1,
    /// <summary>Indicator LED on server / IT equipment.</summary>
    ServerLed           = 2,
    /// <summary>Strip lighting in a breakroom.</summary>
    BreakroomStrip      = 3,
    /// <summary>Track lighting in a conference room.</summary>
    ConferenceTrack     = 4,
    /// <summary>Exterior wall-mounted fixture.</summary>
    ExteriorWall        = 5,
    /// <summary>Backlit signage glow.</summary>
    SignageGlow         = 6,
    /// <summary>Neon tube fixture.</summary>
    Neon                = 7,
    /// <summary>Light from a computer monitor.</summary>
    MonitorGlow         = 8,
    /// <summary>Any other interior fixture not otherwise enumerated.</summary>
    OtherInterior       = 9,
}
