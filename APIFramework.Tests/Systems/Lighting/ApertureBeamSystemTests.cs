using System;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Lighting;
using Xunit;

namespace APIFramework.Tests.Systems.Lighting;

/// <summary>
/// AT-07: South-facing aperture admits a beam at noon (sun azimuth 180°, elevation 90°).
/// AT-08: Any aperture admits no beam when sun elevation ≤ 0.
/// Additional: facing range boundaries; ceiling skylight behaviour.
/// </summary>
public class ApertureBeamSystemTests
{
    private static LightApertureComponent MakeAperture(ApertureFacing facing, double area = 4.0) =>
        new()
        {
            Id          = "apt-test",
            TileX       = 5,
            TileY       = 5,
            RoomId      = "r1",
            Facing      = facing,
            AreaSqTiles = area,
        };

    private static SunStateRecord MakeSun(double azimuth, double elevation) =>
        new(azimuth, elevation, DayPhase.Afternoon);

    // ── AT-07: South-facing at noon ───────────────────────────────────────────

    [Fact]
    public void SouthFacingAperture_AdmitsBeam_AtNoon()
    {
        // Sun at noon: azimuth=180° (south), elevation=90°. South facing accepts ±90° of 180°.
        var sun  = MakeSun(azimuth: 180.0, elevation: 90.0);
        var apt  = MakeAperture(ApertureFacing.South);
        var beam = ApertureBeamSystem.ComputeBeam(sun, apt, colorTemperatureK: 5500);

        Assert.NotNull(beam);
        Assert.True(beam!.Value.Intensity > 0, $"Expected positive beam intensity, got {beam.Value.Intensity}");
    }

    // ── AT-08: No beam when elevation ≤ 0 ────────────────────────────────────

    [Theory]
    [InlineData(ApertureFacing.North)]
    [InlineData(ApertureFacing.East)]
    [InlineData(ApertureFacing.South)]
    [InlineData(ApertureFacing.West)]
    [InlineData(ApertureFacing.Ceiling)]
    public void AnyAperture_NoBeam_WhenElevationAtOrBelowHorizon(ApertureFacing facing)
    {
        var sunAtHorizon = MakeSun(azimuth: 180.0, elevation: 0.0);
        var sunAtNight   = MakeSun(azimuth: 0.0,   elevation: -45.0);
        var apt          = MakeAperture(facing);

        Assert.Null(ApertureBeamSystem.ComputeBeam(sunAtHorizon, apt, 4000));
        Assert.Null(ApertureBeamSystem.ComputeBeam(sunAtNight,   apt, 4000));
    }

    // ── Facing direction acceptance logic ─────────────────────────────────────

    [Fact]
    public void NorthFacingAperture_AdmitsBeam_WhenSunIsInNorth()
    {
        // Sun at azimuth=45° (northeast), elevation=45° — within ±90° of north (0°).
        var sun  = MakeSun(azimuth: 45.0, elevation: 45.0);
        var apt  = MakeAperture(ApertureFacing.North);
        var beam = ApertureBeamSystem.ComputeBeam(sun, apt, 4200);

        Assert.NotNull(beam);
        Assert.True(beam!.Value.Intensity > 0);
    }

    [Fact]
    public void NorthFacingAperture_NoBeam_WhenSunIsInSouth()
    {
        // Sun at azimuth=180° (south), elevation=90° — outside the ±90° north window.
        var sun  = MakeSun(azimuth: 180.0, elevation: 90.0);
        var apt  = MakeAperture(ApertureFacing.North);
        var beam = ApertureBeamSystem.ComputeBeam(sun, apt, 5500);

        Assert.Null(beam);
    }

    [Fact]
    public void EastFacingAperture_AdmitsBeam_WhenSunIsInEast()
    {
        // Sun at azimuth=90° (east), elevation=45° — exactly on-axis for east facing.
        var sun  = MakeSun(azimuth: 90.0, elevation: 45.0);
        var apt  = MakeAperture(ApertureFacing.East);
        var beam = ApertureBeamSystem.ComputeBeam(sun, apt, 4500);

        Assert.NotNull(beam);
        Assert.True(beam!.Value.Intensity > 0);
    }

    [Fact]
    public void WestFacingAperture_AdmitsBeam_AtSunset()
    {
        // Sun at azimuth=270° (west), elevation=30°.
        var sun  = MakeSun(azimuth: 270.0, elevation: 30.0);
        var apt  = MakeAperture(ApertureFacing.West);
        var beam = ApertureBeamSystem.ComputeBeam(sun, apt, 3500);

        Assert.NotNull(beam);
        Assert.True(beam!.Value.Intensity > 0);
    }

    [Fact]
    public void CeilingAperture_AdmitsBeam_WhenElevationAbove30()
    {
        var sun  = MakeSun(azimuth: 0.0, elevation: 45.0); // any azimuth, elevation > 30°
        var apt  = MakeAperture(ApertureFacing.Ceiling);
        var beam = ApertureBeamSystem.ComputeBeam(sun, apt, 5000);

        Assert.NotNull(beam);
        Assert.True(beam!.Value.Intensity > 0);
    }

    [Fact]
    public void CeilingAperture_NoBeam_WhenElevationAt30OrBelow()
    {
        var sun30 = MakeSun(azimuth: 0.0, elevation: 30.0);
        var sun20 = MakeSun(azimuth: 0.0, elevation: 20.0);
        var apt   = MakeAperture(ApertureFacing.Ceiling);

        Assert.Null(ApertureBeamSystem.ComputeBeam(sun30, apt, 5000));
        Assert.Null(ApertureBeamSystem.ComputeBeam(sun20, apt, 5000));
    }

    // ── Intensity scales with AreaSqTiles ─────────────────────────────────────

    [Fact]
    public void LargerArea_ProducesHigherBeamIntensity()
    {
        var sun    = MakeSun(azimuth: 180.0, elevation: 90.0);
        var small  = MakeAperture(ApertureFacing.South, area: 1.0);
        var large  = MakeAperture(ApertureFacing.South, area: 16.0);

        var beamSmall = ApertureBeamSystem.ComputeBeam(sun, small, 5500);
        var beamLarge = ApertureBeamSystem.ComputeBeam(sun, large, 5500);

        Assert.NotNull(beamSmall);
        Assert.NotNull(beamLarge);
        Assert.True(beamLarge!.Value.Intensity > beamSmall!.Value.Intensity,
            $"Large area ({beamLarge.Value.Intensity}) should be brighter than small area ({beamSmall.Value.Intensity})");
    }

    // ── Color temperature is propagated ──────────────────────────────────────

    [Fact]
    public void BeamColorTemperature_MatchesSuppliedValue()
    {
        var sun  = MakeSun(azimuth: 180.0, elevation: 90.0);
        var apt  = MakeAperture(ApertureFacing.South);
        var beam = ApertureBeamSystem.ComputeBeam(sun, apt, colorTemperatureK: 5500);

        Assert.Equal(5500, beam!.Value.ColorTemperatureK);
    }
}
