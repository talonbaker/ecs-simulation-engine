using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-02: Daytime beams from window apertures are visible and angle-correct.
///
/// Tests inject a controlled WorldStateDto via BeamRenderer.InjectWorldState() so no
/// full engine boot is required.  This lets tests verify the beam-visibility logic
/// deterministically regardless of whether office-starter.json is present.
/// </summary>
[TestFixture]
public class BeamRendererSunlitTests
{
    private GameObject   _beamGo;
    private BeamRenderer _beamRenderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _beamGo       = new GameObject("BeamSunlit_Renderer");
        _beamRenderer = _beamGo.AddComponent<BeamRenderer>();
        yield return null;   // Awake()
    }

    [TearDown]
    public void TearDown()
    {
        DestroyAll("BeamSunlit_");
    }

    // ── AT-02a: South-facing aperture is visible at noon ──────────────────────

    [UnityTest]
    public IEnumerator SouthFacingAperture_VisibleAtNoon()
    {
        // Sun at azimuth 180° (south), elevation 60° (afternoon).
        _beamRenderer.InjectWorldState(BuildWorldState(
            sun:       BuildSun(azimuth: 180f, elevation: 60f, DayPhase.Afternoon),
            apertures: new[] { BuildAperture("ap-south", ApertureFacing.South, 3, 3) }));

        yield return null;   // Update()

        Assert.IsTrue(_beamRenderer.IsBeamVisible("ap-south"),
            "South-facing aperture should be visible when sun is at azimuth 180° (south).");
        Assert.Greater(_beamRenderer.GetBeamAlpha("ap-south"), 0f,
            "Beam alpha should be > 0 for a sun-facing aperture.");
    }

    // ── AT-02b: North-facing aperture NOT visible when sun is south ───────────

    [UnityTest]
    public IEnumerator NorthFacingAperture_NotVisibleWhenSunIsSouth()
    {
        _beamRenderer.InjectWorldState(BuildWorldState(
            sun:       BuildSun(azimuth: 180f, elevation: 60f, DayPhase.Afternoon),
            apertures: new[] { BuildAperture("ap-north", ApertureFacing.North, 3, 3) }));

        yield return null;   // Update()

        Assert.IsFalse(_beamRenderer.IsBeamVisible("ap-north"),
            "North-facing aperture should NOT be visible when sun is at 180° (south).");
    }

    // ── AT-02c: East-facing aperture visible at morning (sun at ~90°) ─────────

    [UnityTest]
    public IEnumerator EastFacingAperture_VisibleAtMidMorning()
    {
        _beamRenderer.InjectWorldState(BuildWorldState(
            sun:       BuildSun(azimuth: 90f, elevation: 25f, DayPhase.MidMorning),
            apertures: new[] { BuildAperture("ap-east", ApertureFacing.East, 5, 5) }));

        yield return null;   // Update()

        Assert.IsTrue(_beamRenderer.IsBeamVisible("ap-east"),
            "East-facing aperture should be visible in mid-morning (sun at ~90°).");
        Assert.Greater(_beamRenderer.GetBeamAlpha("ap-east"), 0f);
    }

    // ── AT-02d: West-facing aperture NOT visible in morning ───────────────────

    [UnityTest]
    public IEnumerator WestFacingAperture_NotVisibleAtMorning()
    {
        _beamRenderer.InjectWorldState(BuildWorldState(
            sun:       BuildSun(azimuth: 90f, elevation: 25f, DayPhase.MidMorning),
            apertures: new[] { BuildAperture("ap-west", ApertureFacing.West, 5, 5) }));

        yield return null;   // Update()

        Assert.IsFalse(_beamRenderer.IsBeamVisible("ap-west"),
            "West-facing aperture should NOT be visible in the morning.");
    }

    // ── AT-02e: Multiple apertures — only sun-facing ones are visible ─────────

    [UnityTest]
    public IEnumerator MultipleApertures_OnlySunFacingAreVisible()
    {
        // Sun is at noon (azimuth 180° = south).
        // South and East apertures should be visible (dot > 0); North and West should not.
        _beamRenderer.InjectWorldState(BuildWorldState(
            sun: BuildSun(azimuth: 180f, elevation: 70f, DayPhase.Afternoon),
            apertures: new[]
            {
                BuildAperture("ap-n", ApertureFacing.North, 1, 1),
                BuildAperture("ap-s", ApertureFacing.South, 2, 2),
                BuildAperture("ap-e", ApertureFacing.East,  3, 3),
                BuildAperture("ap-w", ApertureFacing.West,  4, 4),
            }));

        yield return null;   // Update()

        Assert.IsFalse(_beamRenderer.IsBeamVisible("ap-n"), "North: not visible at noon.");
        Assert.IsTrue (_beamRenderer.IsBeamVisible("ap-s"), "South: visible at noon.");
        Assert.IsFalse(_beamRenderer.IsBeamVisible("ap-e"), "East:  not visible at noon.");
        Assert.IsFalse(_beamRenderer.IsBeamVisible("ap-w"), "West:  not visible at noon.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SunStateDto BuildSun(float azimuth, float elevation, DayPhase phase)
        => new SunStateDto
        {
            AzimuthDeg   = azimuth,
            ElevationDeg = elevation,
            DayPhase     = phase,
        };

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
