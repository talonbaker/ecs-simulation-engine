namespace Warden.Contracts.Telemetry;

// -- Room (spatial pillar v0.3) ------------------------------------------------

public sealed record RoomDto
{
    public string        Id          { get; init; } = string.Empty;
    public string        Name        { get; init; } = string.Empty;
    public RoomCategory  Category    { get; init; }
    public BuildingFloor Floor       { get; init; }
    public BoundsRectDto BoundsRect  { get; init; } = default!;
    public IlluminationDto Illumination { get; init; } = default!;
}

public sealed record BoundsRectDto
{
    public int X      { get; init; }
    public int Y      { get; init; }
    public int Width  { get; init; }
    public int Height { get; init; }
}

public sealed record IlluminationDto
{
    public int     AmbientLevel      { get; init; }
    public int     ColorTemperatureK { get; init; }
    public string? DominantSourceId  { get; init; }
}

// -- Enums ---------------------------------------------------------------------

/// <summary>Room functional category. Serialises as camelCase string.</summary>
public enum RoomCategory
{
    Breakroom,
    Bathroom,
    CubicleGrid,
    Office,
    ConferenceRoom,
    SupplyCloset,
    ItCloset,
    Hallway,
    Stairwell,
    Elevator,
    ParkingLot,
    SmokingArea,
    LoadingDock,
    ProductionFloor,
    Lobby,
    Outdoor
}

/// <summary>Building floor. Serialises as camelCase string.</summary>
public enum BuildingFloor
{
    Basement,
    First,
    Top,
    Exterior
}
