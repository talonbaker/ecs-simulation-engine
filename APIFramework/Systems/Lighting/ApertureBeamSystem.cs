using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems.Lighting;

/// <summary>
/// Phase: Lighting (7) — computes per-aperture beam contributions each tick.
///
/// For each aperture with LightApertureTag:
///   - If sun elevation ≤ 0 → no beam.
///   - Otherwise, check if sun azimuth falls within ±90° of the aperture's facing direction.
///     Ceiling apertures accept any azimuth when elevation > 30°.
///   - Within the accepted range, beam intensity = sin(elevation) × cos(off-axis angle) × AreaSqTiles × 10.
///   - Beam color temperature interpolates 4000K (dawn) → 5500K (noon) → 3000K (dusk).
///
/// Results stored in an internal cache; IlluminationAccumulationSystem reads them via GetBeamState.
/// </summary>
/// <remarks>
/// Reads <see cref="SunStateService"/> and <c>LightApertureComponent</c>; writes only to its
/// internal <c>_beamStates</c> cache. Must run AFTER <see cref="SunSystem"/> (so the sun
/// state is fresh) and BEFORE <see cref="IlluminationAccumulationSystem"/>.
/// </remarks>
public sealed class ApertureBeamSystem : ISystem
{
    private readonly SunStateService _sunService;
    private readonly SimulationClock _clock;

    private readonly Dictionary<Entity, ApertureBeamState> _beamStates = new();

    // Facing directions in degrees (north=0, east=90, south=180, west=270)
    private static readonly double[] FacingDeg = { 0.0, 90.0, 180.0, 270.0 };

    /// <summary>
    /// Stores the sun-state and clock references used per tick.
    /// </summary>
    /// <param name="sunService">Singleton sun-state service — provides azimuth and elevation.</param>
    /// <param name="clock">Simulation clock — used to compute beam color temperature from day fraction.</param>
    public ApertureBeamSystem(SunStateService sunService, SimulationClock clock)
    {
        _sunService = sunService;
        _clock      = clock;
    }

    /// <summary>
    /// Returns the beam state for <paramref name="entity"/> from the most recent tick.
    /// Returns null (no beam) if the aperture is not in the cache.
    /// </summary>
    public ApertureBeamState? GetBeamState(Entity entity) =>
        _beamStates.TryGetValue(entity, out var s) ? s : null;

    /// <summary>
    /// Per-tick entry point. Recomputes beam state for every aperture and caches the
    /// results for <see cref="IlluminationAccumulationSystem"/> to consume.
    /// </summary>
    /// <param name="em">Entity manager — queried for apertures.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused; computation is clock-based).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        _beamStates.Clear();

        var sun         = _sunService.CurrentSunState;
        double dayFrac  = _clock.GameTimeOfDay / SimulationClock.SecondsPerDay;
        int    colorK   = ComputeBeamColorTemperature(dayFrac);

        foreach (var entity in em.Query<LightApertureTag>())
        {
            var aperture = entity.Get<LightApertureComponent>();
            var beam     = ComputeBeam(sun, aperture, colorK);
            if (beam.HasValue)
                _beamStates[entity] = beam.Value;
        }
    }

    /// <summary>Pure beam computation — exposed for unit tests.</summary>
    /// <param name="sun">Current sun position.</param>
    /// <param name="aperture">Aperture geometry and facing.</param>
    /// <param name="colorTemperatureK">Color temperature to stamp on the result.</param>
    /// <returns>The beam state, or null if the aperture admits no light at this sun position.</returns>
    public static ApertureBeamState? ComputeBeam(
        SunStateRecord      sun,
        LightApertureComponent aperture,
        int                 colorTemperatureK)
    {
        if (sun.ElevationDeg <= 0.0)
            return null; // sun below horizon → no beam

        double elevRad = sun.ElevationDeg * Math.PI / 180.0;

        if (aperture.Facing == ApertureFacing.Ceiling)
        {
            // Skylight admits any azimuth when elevation > 30°
            if (sun.ElevationDeg <= 30.0)
                return null;

            double intensity = Math.Clamp(
                Math.Sin(elevRad) * aperture.AreaSqTiles * 10.0,
                0.0, 100.0);
            return new ApertureBeamState(intensity, colorTemperatureK);
        }

        // Cardinal facing: check if sun azimuth is within ±90° of facing direction
        double facingDeg  = FacingDeg[(int)aperture.Facing];
        double angleDiff  = Math.Abs(AngleDiff(sun.AzimuthDeg, facingDeg));

        if (angleDiff > 90.0)
            return null; // sun not in aperture's half-space

        double offAxisRad = angleDiff * Math.PI / 180.0;
        double intensity2 = Math.Clamp(
            Math.Sin(elevRad) * Math.Cos(offAxisRad) * aperture.AreaSqTiles * 10.0,
            0.0, 100.0);

        return new ApertureBeamState(intensity2, colorTemperatureK);
    }

    /// <summary>
    /// Signed angular difference from <paramref name="target"/> to <paramref name="source"/>,
    /// in degrees, normalised to (−180, +180].
    /// </summary>
    private static double AngleDiff(double source, double target)
    {
        double diff = (source - target) % 360.0;
        if (diff > 180.0)  diff -= 360.0;
        if (diff <= -180.0) diff += 360.0;
        return diff;
    }

    private static int ComputeBeamColorTemperature(double dayFraction)
    {
        // Sunrise (0.25) → 4000K; noon (0.50) → 5500K; sunset (0.75) → 3000K
        if (dayFraction <= 0.25 || dayFraction >= 0.75)
            return 4000; // pre-sunrise / post-sunset guard (beam shouldn't exist, but safe default)

        if (dayFraction <= 0.50)
        {
            double t = (dayFraction - 0.25) / 0.25; // 0..1
            return (int)(4000 + t * 1500);           // 4000K → 5500K
        }
        else
        {
            double t = (dayFraction - 0.50) / 0.25; // 0..1
            return (int)(5500 - t * 2500);           // 5500K → 3000K
        }
    }
}
