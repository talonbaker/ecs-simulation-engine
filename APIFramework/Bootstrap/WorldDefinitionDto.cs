using System;

namespace APIFramework.Bootstrap;

// Internal DTOs for deserializing world-definition.json.
// All fields are strings for enum-like values; the loader converts to C# types.

/// <summary>Root deserialization target for world-definition.json.</summary>
internal sealed class WorldDefinitionDto
{
    /// <summary>Schema version string declared by the JSON file.</summary>
    public string                   SchemaVersion    { get; set; } = "";
    /// <summary>Stable identifier for the world (informational).</summary>
    public string                   WorldId          { get; set; } = "";
    /// <summary>Human-readable world name.</summary>
    public string                   Name             { get; set; } = "";
    /// <summary>Seed used by the simulation's <c>SeededRandom</c> for deterministic spawning.</summary>
    public int                      Seed             { get; set; }
    /// <summary>Floor metadata blocks; combined with rooms to resolve <c>BuildingFloor</c>.</summary>
    public FloorDefinitionDto[]     Floors           { get; set; } = Array.Empty<FloorDefinitionDto>();
    /// <summary>Rooms to spawn as room entities.</summary>
    public RoomDefinitionDto[]      Rooms            { get; set; } = Array.Empty<RoomDefinitionDto>();
    /// <summary>Light sources (lamps, monitors, fluorescents) to spawn.</summary>
    public LightSourceDefDto[]      LightSources     { get; set; } = Array.Empty<LightSourceDefDto>();
    /// <summary>Light apertures (windows, skylights) to spawn.</summary>
    public LightApertureDefDto[]    LightApertures   { get; set; } = Array.Empty<LightApertureDefDto>();
    /// <summary>NPC slot markers consumed later by the cast generator.</summary>
    public NpcSlotDto[]             NpcSlots         { get; set; } = Array.Empty<NpcSlotDto>();
    /// <summary>Anchor-object placements (one object per room anchor).</summary>
    public AnchorObjectDto[]        ObjectsAtAnchors { get; set; } = Array.Empty<AnchorObjectDto>();
}

/// <summary>One row of the floors block — maps a floor id to a <c>BuildingFloor</c> enum value.</summary>
internal sealed class FloorDefinitionDto
{
    /// <summary>Unique floor identifier referenced by rooms.</summary>
    public string Id        { get; set; } = "";
    /// <summary>Human-readable floor name.</summary>
    public string Name      { get; set; } = "";
    /// <summary>Floor enum string ("basement", "first", "top", "exterior").</summary>
    public string FloorEnum { get; set; } = "";
}

/// <summary>Definition of a single room entity to spawn.</summary>
internal sealed class RoomDefinitionDto
{
    /// <summary>Unique room identifier referenced by light sources, apertures, NPC slots, and objects.</summary>
    public string                 Id                  { get; set; } = "";
    /// <summary>Human-readable room name shown in UI/logs.</summary>
    public string                 Name                { get; set; } = "";
    /// <summary>Room category enum string (e.g. "office", "breakroom") parsed by the loader.</summary>
    public string                 Category            { get; set; } = "";
    /// <summary>Identifier of the floor this room belongs to; must exist in <see cref="WorldDefinitionDto.Floors"/>.</summary>
    public string                 FloorId             { get; set; } = "";
    /// <summary>Tile-space bounds of the room.</summary>
    public BoundsRectDto          Bounds              { get; set; } = new();
    /// <summary>Initial ambient illumination (level + color temperature).</summary>
    public InitialIlluminationDto InitialIllumination { get; set; } = new();
    /// <summary>Optional anchor tag — when set, the room receives a <c>NamedAnchorComponent</c>.</summary>
    public string?                NamedAnchorTag      { get; set; }
    /// <summary>Optional description copied into the named-anchor component.</summary>
    public string?                Description         { get; set; }
    /// <summary>Optional smell tag copied into the named-anchor component.</summary>
    public string?                SmellTag            { get; set; }
    /// <summary>Optional notes attached to the room as a <c>NoteComponent</c>.</summary>
    public string[]?              NotesAttached       { get; set; }
}

/// <summary>Definition of a single light-source entity to spawn.</summary>
internal sealed class LightSourceDefDto
{
    /// <summary>Unique light-source identifier.</summary>
    public string       Id               { get; set; } = "";
    /// <summary>Light kind enum string (e.g. "overheadFluorescent", "deskLamp"); parsed by the loader.</summary>
    public string       Kind             { get; set; } = "";
    /// <summary>Light state enum string ("on", "off", "flickering", "dying"); parsed by the loader.</summary>
    public string       State            { get; set; } = "";
    /// <summary>Initial intensity (0–100).</summary>
    public int          Intensity        { get; set; }
    /// <summary>Color temperature in Kelvin.</summary>
    public int          ColorTemperatureK { get; set; }
    /// <summary>Tile-space position of the source.</summary>
    public TilePointDto Position         { get; set; } = new();
    /// <summary>Identifier of the containing room.</summary>
    public string       RoomId           { get; set; } = "";
}

/// <summary>Definition of a single light-aperture entity (window, skylight) to spawn.</summary>
internal sealed class LightApertureDefDto
{
    /// <summary>Unique aperture identifier.</summary>
    public string       Id           { get; set; } = "";
    /// <summary>Tile-space position of the aperture.</summary>
    public TilePointDto Position     { get; set; } = new();
    /// <summary>Identifier of the room the aperture opens into.</summary>
    public string       RoomId       { get; set; } = "";
    /// <summary>Aperture facing direction ("north", "east", "south", "west", "ceiling").</summary>
    public string       Facing       { get; set; } = "";
    /// <summary>Aperture surface area, in square tiles, used for sun-flux contribution.</summary>
    public double       AreaSqTiles  { get; set; }
}

/// <summary>Definition of an NPC slot marker — the cast generator later replaces this with a real NPC entity.</summary>
internal sealed class NpcSlotDto
{
    /// <summary>Unique slot identifier.</summary>
    public string  Id            { get; set; } = "";
    /// <summary>Optional containing room identifier.</summary>
    public string? RoomId        { get; set; }
    /// <summary>Tile X coordinate of the slot.</summary>
    public int     X             { get; set; }
    /// <summary>Tile Y coordinate of the slot.</summary>
    public int     Y             { get; set; }
    /// <summary>Optional archetype id hint; if it doesn't match the catalog a random archetype is chosen.</summary>
    public string? ArchetypeHint { get; set; }
}

/// <summary>Definition of an anchor object placed at a room's named anchor.</summary>
internal sealed class AnchorObjectDto
{
    /// <summary>Unique object identifier.</summary>
    public string Id            { get; set; } = "";
    /// <summary>Identifier of the containing room (must already be defined).</summary>
    public string RoomId        { get; set; } = "";
    /// <summary>Free-text physical description.</summary>
    public string Description   { get; set; } = "";
    /// <summary>Physical state enum string ("present", "present-degraded", "present-greatly-degraded", "absent").</summary>
    public string PhysicalState { get; set; } = "";
}

/// <summary>Tile-space rectangle (origin + dimensions).</summary>
internal sealed class BoundsRectDto
{
    /// <summary>Tile X of the lower-left corner.</summary>
    public int X      { get; set; }
    /// <summary>Tile Y of the lower-left corner.</summary>
    public int Y      { get; set; }
    /// <summary>Width in tiles.</summary>
    public int Width  { get; set; }
    /// <summary>Height in tiles.</summary>
    public int Height { get; set; }
}

/// <summary>Initial illumination snapshot applied to a room at spawn time.</summary>
internal sealed class InitialIlluminationDto
{
    /// <summary>Ambient lighting level (0–100).</summary>
    public int AmbientLevel      { get; set; }
    /// <summary>Color temperature of the ambient light, in Kelvin.</summary>
    public int ColorTemperatureK { get; set; }
}

/// <summary>Integer tile-space (X, Y) point.</summary>
internal sealed class TilePointDto
{
    /// <summary>Tile X coordinate.</summary>
    public int X { get; set; }
    /// <summary>Tile Y coordinate.</summary>
    public int Y { get; set; }
}
