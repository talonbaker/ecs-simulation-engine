using System;

namespace APIFramework.Build;

/// <summary>
/// POCO mirror of <c>docs/c2-content/build/author-mode-palette.json</c>.
/// Defines the modder-extensible palette of authoring tools — rooms, light sources,
/// light apertures — that the WP-4.0.J author mode exposes on top of the existing
/// player-facing build palette.
/// </summary>
public sealed class AuthorModePaletteData
{
    public string                       SchemaVersion  { get; set; } = "";
    public AuthorModeRoomEntry[]        Rooms          { get; set; } = Array.Empty<AuthorModeRoomEntry>();
    public AuthorModeLightSourceEntry[] LightSources   { get; set; } = Array.Empty<AuthorModeLightSourceEntry>();
    public AuthorModeLightApertureEntry[] LightApertures { get; set; } = Array.Empty<AuthorModeLightApertureEntry>();
}

public sealed class AuthorModeRoomEntry
{
    public string Label    { get; set; } = "";
    public string RoomKind { get; set; } = "";
    public string Tooltip  { get; set; } = "";
}

public sealed class AuthorModeLightSourceEntry
{
    public string Label             { get; set; } = "";
    public string Kind              { get; set; } = "";
    public int    DefaultIntensity  { get; set; }
    public int    DefaultTempK      { get; set; }
    public string DefaultState      { get; set; } = "on";
}

public sealed class AuthorModeLightApertureEntry
{
    public string Label       { get; set; } = "";
    public double AreaSqTiles { get; set; }
}
