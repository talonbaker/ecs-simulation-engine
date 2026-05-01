using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-04: LightSourceState.On → halo visible at nominal intensity. Off → halo hidden.
/// </summary>
[TestFixture]
public class LightSourceHaloOnOffTests
{
    private GameObject            _haloGo;
    private LightSourceHaloRenderer _haloRenderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _haloGo       = new GameObject("HaloOnOff_Renderer");
        _haloRenderer = _haloGo.AddComponent<LightSourceHaloRenderer>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        DestroyAll("HaloOnOff_");
    }

    // ── AT-04a: On state → halo visible ───────────────────────────────────────

    [UnityTest]
    public IEnumerator OnState_HaloIsVisible()
    {
        _haloRenderer.InjectWorldState(BuildWorldState(
            BuildSource("src-on", LightState.On, intensity: 80)));

        yield return null;

        Assert.IsTrue(_haloRenderer.IsHaloVisible("src-on"),
            "LightState.On → halo should be visible.");
        Assert.Greater(_haloRenderer.GetHaloAlpha("src-on"), 0f,
            "LightState.On → halo alpha should be > 0.");
    }

    // ── AT-04b: Off state → halo not visible ──────────────────────────────────

    [UnityTest]
    public IEnumerator OffState_HaloIsNotVisible()
    {
        _haloRenderer.InjectWorldState(BuildWorldState(
            BuildSource("src-off", LightState.Off, intensity: 80)));

        yield return null;

        Assert.IsFalse(_haloRenderer.IsHaloVisible("src-off"),
            "LightState.Off → halo should be hidden.");
    }

    // ── AT-04c: Off then On → halo reappears ──────────────────────────────────

    [UnityTest]
    public IEnumerator OffThenOn_HaloReappears()
    {
        const string id = "src-toggle";

        _haloRenderer.InjectWorldState(BuildWorldState(BuildSource(id, LightState.Off, 80)));
        yield return null;
        Assert.IsFalse(_haloRenderer.IsHaloVisible(id), "Off: hidden.");

        _haloRenderer.InjectWorldState(BuildWorldState(BuildSource(id, LightState.On, 80)));
        yield return null;
        Assert.IsTrue(_haloRenderer.IsHaloVisible(id), "On after Off: visible.");
    }

    // ── AT-04d: Intensity 0 + On → halo has minimal size but is logically visible ──

    [UnityTest]
    public IEnumerator IntensityZero_And_On_HaloVisibleAtMinimalAlpha()
    {
        // Intensity = 0 with On state: halo still exists (source is on) but at minimum alpha.
        _haloRenderer.InjectWorldState(BuildWorldState(
            BuildSource("src-zero", LightState.On, intensity: 0)));

        yield return null;

        // On state: the halo GameObject should be active even at intensity 0.
        // Alpha may be at haloMinAlpha (0 by default) so we just check it's visible.
        Assert.IsTrue(_haloRenderer.IsHaloVisible("src-zero"),
            "On state with intensity=0: halo GO should be active.");
    }

    // ── AT-04e: High intensity → higher halo alpha than low intensity ─────────

    [UnityTest]
    public IEnumerator HighIntensity_HasHigherAlpha_ThanLowIntensity()
    {
        _haloRenderer.InjectWorldState(BuildWorldState(
            BuildSource("src-dim",    LightState.On, intensity: 20),
            BuildSource("src-bright", LightState.On, intensity: 100)));

        yield return null;

        float dimAlpha    = _haloRenderer.GetHaloAlpha("src-dim");
        float brightAlpha = _haloRenderer.GetHaloAlpha("src-bright");

        Assert.Greater(brightAlpha, dimAlpha,
            "Bright source (intensity 100) should have higher halo alpha than dim source (intensity 20).");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LightSourceDto BuildSource(string id, LightState state, int intensity)
        => new LightSourceDto
        {
            Id               = id,
            State            = state,
            Intensity        = intensity,
            ColorTemperatureK = 4000,
            Kind             = LightKind.OverheadFluorescent,
            Position         = new TilePointDto { X = 5, Y = 5 },
            RoomId           = "room-1",
        };

    private static WorldStateDto BuildWorldState(params LightSourceDto[] sources)
        => new WorldStateDto
        {
            SchemaVersion = "0.3.0",
            LightSources  = new List<LightSourceDto>(sources),
        };

    private static void DestroyAll(string prefix)
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith(prefix))
                Object.Destroy(go);
    }
}
