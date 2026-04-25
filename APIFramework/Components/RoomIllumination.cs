namespace APIFramework.Components;

/// <summary>
/// Snapshot illumination state for a room. Populated by the lighting engine (WP-1.2.A);
/// set manually in tests and at spawn time until then.
/// </summary>
/// <param name="AmbientLevel">0–100 where 0 = pitch black and 100 = fully lit.</param>
/// <param name="ColorTemperatureK">1000–10000 Kelvin.</param>
/// <param name="DominantSourceId">Id of the dominant light source entity, or null.</param>
public readonly record struct RoomIllumination(
    int     AmbientLevel,
    int     ColorTemperatureK,
    string? DominantSourceId);
