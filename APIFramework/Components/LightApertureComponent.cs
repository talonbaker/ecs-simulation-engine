namespace APIFramework.Components;

/// <summary>
/// Window or skylight that admits sunlight. Field names, types, and units mirror
/// Warden.Contracts.Telemetry.LightApertureDto exactly.
/// </summary>
public struct LightApertureComponent
{
    /// <summary>UUID string.</summary>
    public string        Id          { get; set; }

    /// <summary>Tile position of the aperture.</summary>
    public int           TileX       { get; set; }

    /// <summary>Tile position of the aperture.</summary>
    public int           TileY       { get; set; }

    /// <summary>Id of the room this aperture admits light into.</summary>
    public string        RoomId      { get; set; }

    /// <summary>Which direction the window faces outward.</summary>
    public ApertureFacing Facing     { get; set; }

    /// <summary>Window area in square tiles (0.5–64.0).</summary>
    public double        AreaSqTiles { get; set; }
}
