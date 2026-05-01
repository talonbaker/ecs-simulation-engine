namespace Warden.Telemetry.AsciiMap;

#if WARDEN

/// <summary>Options for <see cref="AsciiMapProjector.Render"/>.</summary>
public readonly record struct AsciiMapOptions(
    int  FloorIndex     = 0,
    bool IncludeLegend  = true,
    bool ShowHazards    = true,
    bool ShowFurniture  = true,
    bool ShowNpcs       = true);

#endif
