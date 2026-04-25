namespace APIFramework.Components;

/// <summary>
/// Interior light fixture. Field names, types, and units mirror
/// Warden.Contracts.Telemetry.LightSourceDto exactly.
/// </summary>
public struct LightSourceComponent
{
    /// <summary>UUID string.</summary>
    public string    Id               { get; set; }

    /// <summary>Fixture type.</summary>
    public LightKind Kind             { get; set; }

    /// <summary>Operational state. LightSourceStateSystem ticks Flickering and Dying.</summary>
    public LightState State           { get; set; }

    /// <summary>Nominal intensity 0–100. Not modified during Flickering; modified (decremented) during Dying.</summary>
    public int       Intensity        { get; set; }

    /// <summary>Color temperature in Kelvin (1000–10000).</summary>
    public int       ColorTemperatureK { get; set; }

    /// <summary>Tile position of the fixture (X, Y).</summary>
    public int       TileX            { get; set; }

    /// <summary>Tile position of the fixture (X, Y).</summary>
    public int       TileY            { get; set; }

    /// <summary>Id of the room this source illuminates.</summary>
    public string    RoomId           { get; set; }
}
