namespace Warden.Contracts.Telemetry;

// -- Light source (spatial pillar v0.3) ---------------------------------------

public sealed record LightSourceDto
{
    public string       Id               { get; init; } = string.Empty;
    public LightKind    Kind             { get; init; }
    public LightState   State            { get; init; }
    public int          Intensity        { get; init; }
    public int          ColorTemperatureK { get; init; }
    public TilePointDto Position         { get; init; } = default!;
    public string       RoomId           { get; init; } = string.Empty;
}

// -- Enums ---------------------------------------------------------------------

/// <summary>Interior light-source type. Serialises as camelCase string.</summary>
public enum LightKind
{
    OverheadFluorescent,
    DeskLamp,
    ServerLed,
    BreakroomStrip,
    ConferenceTrack,
    ExteriorWall,
    SignageGlow,
    Neon,
    MonitorGlow,
    OtherInterior
}

/// <summary>Operational state of a light source. Serialises as camelCase string.</summary>
public enum LightState
{
    On,
    Off,
    Flickering,
    Dying
}
