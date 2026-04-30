namespace APIFramework.Systems.Lighting;

/// <summary>
/// Per-tick cached beam contribution for a light aperture.
/// Read by IlluminationAccumulationSystem each tick.
/// </summary>
/// <param name="Intensity">Beam intensity contribution (0–100 scale, pre-falloff).</param>
/// <param name="ColorTemperatureK">Beam color temperature in Kelvin, interpolated by time of day.</param>
public readonly record struct ApertureBeamState(
    double Intensity,
    int    ColorTemperatureK);
