using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-11: Performance gate — 30 NPCs with FULL LIGHTING at 60 seconds real-time.
/// FPS sampled at 1 Hz.  Thresholds: min ≥ 55, mean ≥ 58, p99 ≥ 50.
///
/// DO NOT WEAKEN THESE THRESHOLDS TO SHIP.
///
/// This test adds the WP-3.1.C lighting stack on top of the 3.1.A base:
///   - RoomRectangleRenderer (with walls)
///   - NpcDotRenderer
///   - RoomAmbientTintApplier
///   - BeamRenderer
///   - LightSourceHaloRenderer
///   - DayNightCycleRenderer
///   - WallFadeController
///
/// If this test fails, escalate as blocked with measured min/mean/p99 values.
/// Profile suspects: WallTag.FindObjectsOfType, beam/halo material updates, shader compile.
///
/// Mark [Explicit] to keep it out of default CI:
///   [UnityTest, Explicit("Performance gate — 60 s, run manually before release")]
/// </summary>
[TestFixture]
public class PerformanceGate30NpcWithLightingTests
{
    private const float MinFps       = 55f;
    private const float MeanFps      = 58f;
    private const float P99Fps       = 50f;
    private const float TestDuration = 60f;
    private const float WarmUp       = 3f;

    private FrameRateMonitor _monitor;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // ── Engine host ───────────────────────────────────────────────────────
        var hostGo = new GameObject("PerfLighting_Host");
        var host   = hostGo.AddComponent<EngineHost>();

        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        SetField(host, "_configAsset",         configAsset);
        SetField(host, "_worldDefinitionPath", "office-starter.json");

        // ── Directional light ────────────────────────────────────────────────
        var lightGo = new GameObject("PerfLighting_DirLight");
        var dirLight = lightGo.AddComponent<Light>();
        dirLight.type = LightType.Directional;

        // ── Room renderer (with walls from WP-3.1.C) ─────────────────────────
        var roomGo   = new GameObject("PerfLighting_RoomRenderer");
        var roomRend = roomGo.AddComponent<RoomRectangleRenderer>();
        SetField(roomRend, "_engineHost", host);

        // ── NPC dot renderer ──────────────────────────────────────────────────
        var npcGo   = new GameObject("PerfLighting_NpcDotRenderer");
        var npcRend = npcGo.AddComponent<NpcDotRenderer>();
        SetField(npcRend, "_engineHost", host);

        // ── Ambient tint ──────────────────────────────────────────────────────
        var tintGo = new GameObject("PerfLighting_TintApplier");
        var tint   = tintGo.AddComponent<RoomAmbientTintApplier>();
        SetField(tint, "_engineHost",   host);
        SetField(tint, "_roomRenderer", roomRend);

        // ── Beam renderer ─────────────────────────────────────────────────────
        var beamGo = new GameObject("PerfLighting_BeamRenderer");
        var beam   = beamGo.AddComponent<BeamRenderer>();
        SetField(beam, "_engineHost", host);

        // ── Light source halo renderer ────────────────────────────────────────
        var haloGo = new GameObject("PerfLighting_HaloRenderer");
        var halo   = haloGo.AddComponent<LightSourceHaloRenderer>();
        SetField(halo, "_engineHost", host);

        // ── Day-night cycle renderer ──────────────────────────────────────────
        var cycleGo = new GameObject("PerfLighting_DayNight");
        var cycle   = cycleGo.AddComponent<DayNightCycleRenderer>();
        SetField(cycle, "_directionalLight", dirLight);
        SetField(cycle, "_engineHost", host);

        // ── Camera and wall fade ──────────────────────────────────────────────
        var camGo    = new GameObject("PerfLighting_Camera");
        camGo.AddComponent<Camera>();
        var camCtrl  = camGo.AddComponent<CameraController>();

        var wfcGo    = new GameObject("PerfLighting_WallFade");
        var wallFade = wfcGo.AddComponent<WallFadeController>();
        SetField(wallFade, "_cameraController", camCtrl);

        // ── FPS monitor ───────────────────────────────────────────────────────
        _monitor = new GameObject("PerfLighting_Monitor").AddComponent<FrameRateMonitor>();

        yield return null;   // Start()
        yield return null;   // first Update()
    }

    [TearDown]
    public void TearDown()
    {
        DestroyAll("PerfLighting_");
    }

    [UnityTest]
    [Explicit("Performance gate — 60 s, run manually before release")]
    public IEnumerator FullLighting_30Npcs_FrameRateMeetsGate()
    {
        // Warm-up: discard initial frames (shader compilation, asset loading).
        float warmupElapsed = 0f;
        while (warmupElapsed < WarmUp)
        {
            warmupElapsed += Time.deltaTime;
            yield return null;
        }

        _monitor.StartRecording();

        // Sample for 60 seconds.
        float elapsed = 0f;
        while (elapsed < TestDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        _monitor.StopRecording();

        float min  = _monitor.MinFps;
        float mean = _monitor.MeanFps;
        float p99  = _monitor.P99Fps;

        UnityEngine.Debug.Log(
            $"[PerfGate-3.1.C] Lighting + 30 NPC — " +
            $"min={min:F1} mean={mean:F1} p99={p99:F1}");

        Assert.GreaterOrEqual(min,  MinFps,
            $"Min FPS {min:F1} < {MinFps} — lighting violates performance gate.");
        Assert.GreaterOrEqual(mean, MeanFps,
            $"Mean FPS {mean:F1} < {MeanFps} — lighting violates performance gate.");
        Assert.GreaterOrEqual(p99,  P99Fps,
            $"P99 FPS {p99:F1} < {P99Fps} — lighting violates performance gate.");
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
