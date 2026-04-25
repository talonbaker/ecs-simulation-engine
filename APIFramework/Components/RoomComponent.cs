namespace APIFramework.Components;

/// <summary>
/// Identifies a room entity. Field names, types, and units mirror
/// <c>Warden.Contracts.Telemetry.RoomDto</c> exactly so the projector can map
/// field-by-field without a translation layer.
/// </summary>
public struct RoomComponent
{
    /// <summary>UUID string, mirrors RoomDto.Id.</summary>
    public string Id { get; set; }

    /// <summary>Slug, max 64 chars, mirrors RoomDto.Name.</summary>
    public string Name { get; set; }

    /// <summary>Functional category, mirrors RoomDto.Category.</summary>
    public RoomCategory Category { get; set; }

    /// <summary>Which floor of the building, mirrors RoomDto.Floor.</summary>
    public BuildingFloor Floor { get; set; }

    /// <summary>Axis-aligned tile-unit bounds, mirrors RoomDto.BoundsRect.</summary>
    public BoundsRect Bounds { get; set; }

    /// <summary>Current illumination snapshot, mirrors RoomDto.Illumination.</summary>
    public RoomIllumination Illumination { get; set; }
}
