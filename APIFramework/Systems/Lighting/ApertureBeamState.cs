namespace APIFramework.Systems.Lighting;

/// <summary>
/// Per-tick cached beam contribution for a light aperture.
/// Read by IlluminationAccumulationSystem each tick.
/// </summary>
public readonly record struct ApertureBeamState(
    double Intensity,
    int    ColorTemperatureK);
