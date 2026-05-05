namespace Warden.Contracts.Telemetry;

// -- Light aperture / window (spatial pillar v0.3) ----------------------------

public sealed record LightApertureDto
{
    public string         Id          { get; init; } = string.Empty;
    public TilePointDto   Position    { get; init; } = default!;
    public string         RoomId      { get; init; } = string.Empty;
    public ApertureFacing Facing      { get; init; }
    public double         AreaSqTiles { get; init; }
}

// -- Enum ----------------------------------------------------------------------

/// <summary>Cardinal direction a window faces. Serialises as camelCase string.</summary>
public enum ApertureFacing
{
    North,
    East,
    South,
    West,
    Ceiling
}
