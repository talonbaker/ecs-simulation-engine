using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems.Lighting;

/// <summary>
/// Phase: Lighting (7) — first system in the lighting group.
///
/// Reads the simulation clock's day fraction (GameTimeOfDay / SecondsPerDay → 0..1)
/// and computes sun azimuth, elevation, and the six-phase DayPhase enum.
/// Writes the result to SunStateService for downstream lighting systems.
///
/// NOTE: SimulationClock.CircadianFactor is a sleep-urgency multiplier (0.10–1.60),
/// not a 0..1 day fraction. SunSystem reads GameTimeOfDay / SecondsPerDay instead,
/// which is the true 0..1 position in the 24-hour cycle.
/// </summary>
public sealed class SunSystem : ISystem
{
    private readonly SimulationClock  _clock;
    private readonly SunStateService  _service;
    private readonly LightingConfig   _cfg;

    public SunSystem(SimulationClock clock, SunStateService service, LightingConfig cfg)
    {
        _clock   = clock;
        _service = service;
        _cfg     = cfg;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        double dayFraction = _clock.GameTimeOfDay / SimulationClock.SecondsPerDay;
        var    state       = ComputeFromDayFraction(dayFraction, _cfg);
        _service.UpdateSunState(state);
    }

    /// <summary>
    /// Pure computation — exposed for unit tests that supply a day fraction directly.
    /// </summary>
    public static SunStateRecord ComputeFromDayFraction(double dayFraction, LightingConfig cfg)
    {
        // Orbital model (simplified):
        //   azimuth = 360° × dayFraction  (sun at north=0°/360° at midnight, east=90° at 6am,
        //                                   south=180° at noon, west=270° at 6pm)
        //   elevation = 90° × sin(2π × (dayFraction − 0.25))
        //             → +90° at noon (0.5), 0° at sunrise (0.25) / sunset (0.75), −90° at midnight (0.0)
        double azimuthDeg   = 360.0 * dayFraction;
        double elevationDeg = 90.0  * Math.Sin(2.0 * Math.PI * (dayFraction - 0.25));
        DayPhase phase      = ComputeDayPhase(dayFraction, cfg);

        return new SunStateRecord(azimuthDeg, elevationDeg, phase);
    }

    private static DayPhase ComputeDayPhase(double f, LightingConfig cfg)
    {
        var b = cfg.DayPhaseBoundaries;
        if (f >= b.NightStart)                              return DayPhase.Night;
        if (f >= b.DuskStart)                               return DayPhase.Dusk;
        if (f >= b.EveningStart)                            return DayPhase.Evening;
        if (f >= b.AfternoonStart)                          return DayPhase.Afternoon;
        if (f >= b.MidMorningStart)                         return DayPhase.MidMorning;
        if (f >= b.EarlyMorningStart)                       return DayPhase.EarlyMorning;
        return DayPhase.Night; // [0.00, earlyMorningStart)
    }
}
