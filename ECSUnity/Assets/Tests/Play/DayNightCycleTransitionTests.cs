using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-09: Day-night cycle — directional light color shifts continuously across phases.
///
/// Tests inject known sun states via DayNightCycleRenderer.ForceApplySunState() and verify
/// the directional light's color matches the aesthetic-bible §"Time of day" commitments:
///   Night     → cool dark blue
///   Dawn      → warm orange
///   Noon      → neutral white
///   Dusk      → warm orange fading to blue
/// </summary>
[TestFixture]
public class DayNightCycleTransitionTests
{
    private GameObject         _lightGo;
    private Light              _directionalLight;
    private DayNightCycleRenderer _cycleRenderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Create the directional light.
        _lightGo          = new GameObject("DayNight_DirLight");
        _directionalLight = _lightGo.AddComponent<Light>();
        _directionalLight.type = LightType.Directional;

        // Create the cycle renderer.
        var rendererGo = new GameObject("DayNight_Renderer");
        _cycleRenderer = rendererGo.AddComponent<DayNightCycleRenderer>();
        SetField(_cycleRenderer, "_directionalLight", _directionalLight);
        // _engineHost left null — we'll use ForceApplySunState.
        // _config left null — renderer uses fallback values.

        yield return null;   // Start()
    }

    [TearDown]
    public void TearDown()
    {
        DestroyAll("DayNight_");
    }

    // ── AT-09a: Night → cool dark blue ────────────────────────────────────────

    [UnityTest]
    public IEnumerator Night_DirectionalLight_IsCoolBlue()
    {
        _cycleRenderer.ForceApplySunState(
            azimuthDeg: 0f, elevationDeg: -30f, phase: DayPhase.Night);

        yield return null;

        Color c = _directionalLight.color;
        // Night: blue channel should dominate over red.
        Assert.Greater(c.b, c.r,
            $"Night light should be cool blue (b={c.b:F3} > r={c.r:F3}).");
        Assert.Less(_directionalLight.intensity, 0.2f,
            "Night directional light intensity should be very low.");
    }

    // ── AT-09b: Noon → neutral white ──────────────────────────────────────────

    [UnityTest]
    public IEnumerator Noon_DirectionalLight_IsNeutralWhite()
    {
        _cycleRenderer.ForceApplySunState(
            azimuthDeg: 180f, elevationDeg: 80f, phase: DayPhase.Afternoon);

        yield return null;

        Color c = _directionalLight.color;
        // Noon: all channels close to each other (neutral white).
        float range = Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b);
        Assert.Less(range, 0.25f,
            $"Noon light should be near-white (channel range {range:F3}).");
        Assert.Greater(_directionalLight.intensity, 0.7f,
            "Noon directional light should be at high intensity.");
    }

    // ── AT-09c: Early morning → warm orange ───────────────────────────────────

    [UnityTest]
    public IEnumerator EarlyMorning_DirectionalLight_IsWarmOrange()
    {
        _cycleRenderer.ForceApplySunState(
            azimuthDeg: 70f, elevationDeg: 4f, phase: DayPhase.EarlyMorning);

        yield return null;

        Color c = _directionalLight.color;
        // Dawn: red channel dominant (warm orange).
        Assert.Greater(c.r, c.b,
            $"Early morning light should be warm (r={c.r:F3} > b={c.b:F3}).");
    }

    // ── AT-09d: Dusk → warm orange / deep orange ──────────────────────────────

    [UnityTest]
    public IEnumerator Dusk_DirectionalLight_IsWarmOrange()
    {
        _cycleRenderer.ForceApplySunState(
            azimuthDeg: 290f, elevationDeg: 3f, phase: DayPhase.Dusk);

        yield return null;

        Color c = _directionalLight.color;
        Assert.Greater(c.r, c.b,
            $"Dusk light should be warm orange (r={c.r:F3} > b={c.b:F3}).");
    }

    // ── AT-09e: Intensity increases from night to noon ────────────────────────

    [UnityTest]
    public IEnumerator Intensity_IncreasesFromNightToNoon()
    {
        _cycleRenderer.ForceApplySunState(0f, -30f, DayPhase.Night);
        yield return null;
        float nightIntensity = _directionalLight.intensity;

        _cycleRenderer.ForceApplySunState(180f, 80f, DayPhase.Afternoon);
        yield return null;
        float noonIntensity = _directionalLight.intensity;

        Assert.Greater(noonIntensity, nightIntensity,
            $"Noon intensity ({noonIntensity:F3}) should exceed night intensity ({nightIntensity:F3}).");
    }

    // ── AT-09f: Rotation azimuth matches sun azimuth ──────────────────────────

    [UnityTest]
    public IEnumerator DirectionalLight_Rotation_MatchesSunAzimuth()
    {
        _cycleRenderer.ForceApplySunState(
            azimuthDeg: 180f, elevationDeg: 45f, phase: DayPhase.Afternoon);

        yield return null;

        // Light rotation Y should be close to 180°.
        float yaw = _directionalLight.transform.eulerAngles.y;
        Assert.AreEqual(180f, yaw, 5f,
            $"Directional light yaw should match sun azimuth 180° (got {yaw:F1}°).");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetField(object target, string field, object value)
    {
        var f = target.GetType().GetField(field,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f?.SetValue(target, value);
    }

    private static void DestroyAll(string prefix)
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith(prefix))
                Object.Destroy(go);
    }
}
