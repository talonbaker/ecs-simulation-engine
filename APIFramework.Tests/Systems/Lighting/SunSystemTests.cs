using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Systems.Lighting;
using Xunit;

namespace APIFramework.Tests.Systems.Lighting;

/// <summary>
/// AT-02: circadian factor 0.55 → DayPhase.Afternoon.
/// AT-03: elevation positive between sunrise (0.25) and sunset (0.75), negative outside.
/// </summary>
public class SunSystemTests
{
    private static LightingConfig DefaultCfg() => new();

    // -- AT-02 ----------------------------------------------------------------

    [Fact]
    public void SunSystem_AtDayFraction055_ProducesAfternoonPhase()
    {
        var state = SunSystem.ComputeFromDayFraction(0.55, DefaultCfg());
        Assert.Equal(DayPhase.Afternoon, state.DayPhase);
    }

    // -- AT-03 ----------------------------------------------------------------

    [Fact]
    public void SunSystem_BetweenSunriseAndSunset_ElevationIsPositive()
    {
        // Sunrise = 0.25, sunset = 0.75. Test mid-day values well inside the window.
        foreach (double f in new[] { 0.30, 0.40, 0.50, 0.60, 0.70 })
        {
            var state = SunSystem.ComputeFromDayFraction(f, DefaultCfg());
            Assert.True(state.ElevationDeg > 0,
                $"Expected elevation > 0 at dayFraction={f}, got {state.ElevationDeg:F2}°");
        }
    }

    [Fact]
    public void SunSystem_AtNight_ElevationIsNegative()
    {
        // Night values outside [0.25, 0.75]
        foreach (double f in new[] { 0.05, 0.10, 0.80, 0.90 })
        {
            var state = SunSystem.ComputeFromDayFraction(f, DefaultCfg());
            Assert.True(state.ElevationDeg < 0,
                $"Expected elevation < 0 at dayFraction={f}, got {state.ElevationDeg:F2}°");
        }
    }

    // -- Orbital model spot checks ---------------------------------------------

    [Fact]
    public void SunSystem_AtNoon_ElevationNear90()
    {
        var state = SunSystem.ComputeFromDayFraction(0.50, DefaultCfg());
        Assert.Equal(90.0, state.ElevationDeg, precision: 5);
    }

    [Fact]
    public void SunSystem_AtMidnight_ElevationNearNeg90()
    {
        var state = SunSystem.ComputeFromDayFraction(0.0, DefaultCfg());
        Assert.Equal(-90.0, state.ElevationDeg, precision: 5);
    }

    [Fact]
    public void SunSystem_AtNoon_AzimuthNear180()
    {
        var state = SunSystem.ComputeFromDayFraction(0.50, DefaultCfg());
        Assert.Equal(180.0, state.AzimuthDeg, precision: 5);
    }

    // -- Day-phase boundaries -------------------------------------------------

    [Theory]
    [InlineData(0.00,  DayPhase.Night)]
    [InlineData(0.10,  DayPhase.Night)]
    [InlineData(0.20,  DayPhase.EarlyMorning)]
    [InlineData(0.25,  DayPhase.EarlyMorning)]
    [InlineData(0.30,  DayPhase.MidMorning)]
    [InlineData(0.40,  DayPhase.MidMorning)]
    [InlineData(0.45,  DayPhase.Afternoon)]
    [InlineData(0.55,  DayPhase.Afternoon)]
    [InlineData(0.65,  DayPhase.Evening)]
    [InlineData(0.70,  DayPhase.Evening)]
    [InlineData(0.80,  DayPhase.Dusk)]
    [InlineData(0.84,  DayPhase.Dusk)]
    [InlineData(0.85,  DayPhase.Night)]
    [InlineData(0.95,  DayPhase.Night)]
    public void SunSystem_DayPhaseBoundaries_MatchSimConfig(double dayFraction, DayPhase expected)
    {
        var state = SunSystem.ComputeFromDayFraction(dayFraction, DefaultCfg());
        Assert.Equal(expected, state.DayPhase);
    }

    // -- Clock integration ----------------------------------------------------

    [Fact]
    public void SunSystem_Update_WritesSunStateService()
    {
        var clock   = new APIFramework.Core.SimulationClock();
        var service = new SunStateService();
        var sys     = new SunSystem(clock, service, DefaultCfg());
        var em      = new APIFramework.Core.EntityManager();

        sys.Update(em, 1f);

        // Clock starts at 6AM (dayFraction ≈ 0.25), elevation should be near 0
        Assert.True(service.CurrentSunState.ElevationDeg >= -5.0 &&
                    service.CurrentSunState.ElevationDeg <= 5.0,
            $"Expected elevation near 0° at 6AM, got {service.CurrentSunState.ElevationDeg:F2}°");
    }
}
