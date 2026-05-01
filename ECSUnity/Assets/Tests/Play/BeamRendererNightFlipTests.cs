using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-03: Nighttime beam direction reversal — interior light spills outward through apertures.
///
/// When sun elevation is below BeamRenderer's minimum elevation threshold, window beams
/// reverse direction: instead of sun entering the room they show interior light spilling out.
/// This matches the aesthetic-bible §"Time of day — Night: window beams flip" commitment.
/// </summary>
[TestFixture]
public class BeamRendererNightFlipTests
{
    private GameObject   _beamGo;
    private BeamRenderer _beamRenderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _beamGo       = new GameObject("BeamNight_Renderer");
        _beamRenderer = _beamGo.AddComponent<BeamRenderer>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        DestroyAll("BeamNight_");
    }

    // ── AT-03a: At night, ALL apertures are visible (night spill) ─────────────

    [UnityTest]
    public IEnumerator AtNight_AllApertures_AreVisible()
    {
        // Sun elevation = -30° (clearly below the horizon = night).
        _beamRenderer.InjectWorldState(BuildWorldState(
            sun: BuildSun(azimuth: 0f, elevation: -30f, DayPhase.Night),
            apertures: new[]
            {
                BuildAperture("ap-n", ApertureFacing.North, 1, 1),
                BuildAperture("ap-s", ApertureFacing.South, 2, 2),
                BuildAperture("ap-e", ApertureFacing.East,  3, 3),
                BuildAperture("ap-w", ApertureFacing.West,  4, 4),
            }));

        yield return null;

        // At night all apertures show interior-spill beams (visible but low alpha).
        Assert.IsTrue(_beamRenderer.IsBeamVisible("ap-n"), "North aperture visible at night (spill out).");
        Assert.IsTrue(_beamRenderer.IsBeamVisible("ap-s"), "South aperture visible at night (spill out).");
        Assert.IsTrue(_beamRenderer.IsBeamVisible("ap-e"), "East aperture visible at night (spill out).");
        Assert.IsTrue(_beamRenderer.IsBeamVisible("ap-w"), "West aperture visible at night (spill out).");
    }

    // ── AT-03b: Night beams have lower alpha than daytime beams ──────────────

    [UnityTest]
    public IEnumerator NightBeam_AlphaIsLowerThanDaytimeBeam()
    {
        const string apId = "ap-south";

        // Day state: south aperture with sun at 180°.
        _beamRenderer.InjectWorldState(BuildWorldState(
            sun: BuildSun(azimuth: 180f, elevation: 30f, DayPhase.MidMorning),
            apertures: new[] { BuildAperture(apId, ApertureFacing.South, 3, 3) }));

        yield return null;
        float dayAlpha = _beamRenderer.GetBeamAlpha(apId);

        // Night state: sun below horizon.
        _beamRenderer.InjectWorldState(BuildWorldState(
            sun: BuildSun(azimuth: 180f, elevation: -20f, DayPhase.Night),
            apertures: new[] { BuildAperture(apId, ApertureFacing.South, 3, 3) }));

        yield return null;
        float nightAlpha = _beamRenderer.GetBeamAlpha(apId);

        Assert.Greater(dayAlpha, nightAlpha,
            "Day beam alpha should be higher than night spill-out beam alpha.");
        // Night alpha should be small but non-zero (window glow).
        Assert.Greater(nightAlpha, 0f, "Night spill beam should have non-zero alpha.");
        Assert.Less(nightAlpha, 0.25f, "Night spill beam should be low alpha (subtle glow).");
    }

    // ── AT-03c: Transition from day to night - beam changes character ─────────

    [UnityTest]
    public IEnumerator Transition_DayToNight_BeamRemainsVisible()
    {
        const string apId = "ap-east";

        // Dusk: elevation near the threshold.
        _beamRenderer.InjectWorldState(BuildWorldState(
            sun: BuildSun(azimuth: 90f, elevation: 2f, DayPhase.Dusk),
            apertures: new[] { BuildAperture(apId, ApertureFacing.East, 5, 5) }));

        yield return null;
        // At elevation = 2° (below beamMinElevationDeg = 3°), should be night mode.
        bool duskVisible = _beamRenderer.IsBeamVisible(apId);

        // Night: well below horizon.
        _beamRenderer.InjectWorldState(BuildWorldState(
            sun: BuildSun(azimuth: 90f, elevation: -40f, DayPhase.Night),
            apertures: new[] { BuildAperture(apId, ApertureFacing.East, 5, 5) }));

        yield return null;
        bool nightVisible = _beamRenderer.IsBeamVisible(apId);

        Assert.IsTrue(duskVisible,  "Aperture should show night-spill beam at low elevation.");
        Assert.IsTrue(nightVisible, "Aperture should show night-spill beam at night.");
    }

    // ── AT-03d: No sun state in WorldState → treated as night ─────────────────

    [UnityTest]
    public IEnumerator NoSunState_TreatedAsNight_AllAperturesVisible()
    {
        // Clock.Sun = null simulates a world snapshot where sun state wasn't populated.
        _beamRenderer.InjectWorldState(new WorldStateDto
        {
            SchemaVersion  = "0.3.0",
            Clock          = new ClockStateDto { Sun = null },   // no sun data
            LightApertures = new List<LightApertureDto>
            {
                BuildAperture("ap-ceil-skip", ApertureFacing.Ceiling, 5, 5),   // should be skipped
                BuildAperture("ap-s-null",    ApertureFacing.South,   3, 3),
            },
        });

        yield return null;

        // Ceiling apertures are skipped regardless.
        Assert.IsFalse(_beamRenderer.IsBeamVisible("ap-ceil-skip"),
            "Ceiling apertures are always skipped.");
        // Non-ceiling apertures should show as night (spill-out).
        Assert.IsTrue(_beamRenderer.IsBeamVisible("ap-s-null"),
            "Without sun state, non-ceiling apertures should show night-spill beams.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SunStateDto BuildSun(float azimuth, float elevation, DayPhase phase)
        => new SunStateDto { AzimuthDeg = azimuth, ElevationDeg = elevation, DayPhase = phase };

    private static LightApertureDto BuildAperture(string id, ApertureFacing facing, int x, int y)
        => new LightApertureDto
        {
            Id          = id,
            Facing      = facing,
            Position    = new TilePointDto { X = x, Y = y },
            RoomId      = "room-1",
            AreaSqTiles = 4.0,
        };

    private static WorldStateDto BuildWorldState(SunStateDto sun, LightApertureDto[] apertures)
        => new WorldStateDto
        {
            SchemaVersion  = "0.3.0",
            Clock          = new ClockStateDto { Sun = sun },
            LightApertures = new List<LightApertureDto>(apertures),
        };

    private static void DestroyAll(string prefix)
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith(prefix))
                Object.Destroy(go);
    }
}
