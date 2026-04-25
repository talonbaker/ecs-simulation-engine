using System;

namespace APIFramework.Bootstrap;

// Internal DTOs for deserializing world-definition.json.
// All fields are strings for enum-like values; the loader converts to C# types.

internal sealed class WorldDefinitionDto
{
    public string                   SchemaVersion    { get; set; } = "";
    public string                   WorldId          { get; set; } = "";
    public string                   Name             { get; set; } = "";
    public int                      Seed             { get; set; }
    public FloorDefinitionDto[]     Floors           { get; set; } = Array.Empty<FloorDefinitionDto>();
    public RoomDefinitionDto[]      Rooms            { get; set; } = Array.Empty<RoomDefinitionDto>();
    public LightSourceDefDto[]      LightSources     { get; set; } = Array.Empty<LightSourceDefDto>();
    public LightApertureDefDto[]    LightApertures   { get; set; } = Array.Empty<LightApertureDefDto>();
    public NpcSlotDto[]             NpcSlots         { get; set; } = Array.Empty<NpcSlotDto>();
    public AnchorObjectDto[]        ObjectsAtAnchors { get; set; } = Array.Empty<AnchorObjectDto>();
}

internal sealed class FloorDefinitionDto
{
    public string Id        { get; set; } = "";
    public string Name      { get; set; } = "";
    public string FloorEnum { get; set; } = "";
}

internal sealed class RoomDefinitionDto
{
    public string                 Id                  { get; set; } = "";
    public string                 Name                { get; set; } = "";
    public string                 Category            { get; set; } = "";
    public string                 FloorId             { get; set; } = "";
    public BoundsRectDto          Bounds              { get; set; } = new();
    public InitialIlluminationDto InitialIllumination { get; set; } = new();
    public string?                NamedAnchorTag      { get; set; }
    public string?                Description         { get; set; }
    public string?                SmellTag            { get; set; }
    public string[]?              NotesAttached       { get; set; }
}

internal sealed class LightSourceDefDto
{
    public string       Id               { get; set; } = "";
    public string       Kind             { get; set; } = "";
    public string       State            { get; set; } = "";
    public int          Intensity        { get; set; }
    public int          ColorTemperatureK { get; set; }
    public TilePointDto Position         { get; set; } = new();
    public string       RoomId           { get; set; } = "";
}

internal sealed class LightApertureDefDto
{
    public string       Id           { get; set; } = "";
    public TilePointDto Position     { get; set; } = new();
    public string       RoomId       { get; set; } = "";
    public string       Facing       { get; set; } = "";
    public double       AreaSqTiles  { get; set; }
}

internal sealed class NpcSlotDto
{
    public string  Id            { get; set; } = "";
    public string? RoomId        { get; set; }
    public int     X             { get; set; }
    public int     Y             { get; set; }
    public string? ArchetypeHint { get; set; }
}

internal sealed class AnchorObjectDto
{
    public string Id            { get; set; } = "";
    public string RoomId        { get; set; } = "";
    public string Description   { get; set; } = "";
    public string PhysicalState { get; set; } = "";
}

internal sealed class BoundsRectDto
{
    public int X      { get; set; }
    public int Y      { get; set; }
    public int Width  { get; set; }
    public int Height { get; set; }
}

internal sealed class InitialIlluminationDto
{
    public int AmbientLevel      { get; set; }
    public int ColorTemperatureK { get; set; }
}

internal sealed class TilePointDto
{
    public int X { get; set; }
    public int Y { get; set; }
}
