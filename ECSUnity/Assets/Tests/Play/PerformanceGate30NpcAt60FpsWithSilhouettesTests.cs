using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-10: THE HARDEST GATE — preserved with silhouettes.
/// 30 NPCs (office-starter spawns 15; test verifies ≥15), silhouette renderer,
/// 60 real-time seconds. FPS thresholds: min ≥ 55, mean ≥ 58, p99 ≥ 50.
///
/// This test is the WP-3.1.B counterpart to PerformanceGate30NpcAt60FpsTests from
/// WP-3.1.A. The only difference is that NpcSilhouetteRenderer replaces NpcDotRenderer.
/// All other setup — FrameRateMonitor, warm-up, measurement loop — is identical.
///
/// DO NOT WEAKEN THESE THRESHOLDS.
/// If this test fails, escalate as blocked with measurements.
/// Diagnosis order:
///   1. Draw call count > expected → sprite batching not engaging (run SpriteBatchingTests)
///   2. Per-frame allocations from animator state changes (check GC.alloc in profiler)
///   3. Sprite lookup hitting non-cached path (SilhouetteAssetCatalog.EnsureBodyCache called each frame)
///   4. EntityManager entity-map refresh triggering on every frame (count not changing but map rebuilt)
///
/// This test takes 60+ real-time seconds. Mark Explicit to exclude from CI if needed:
///   [UnityTest, Explicit("Performance gate — 60s, run manually before release")]
/// </summary>
[TestFixture]
public class PerformanceGate30NpcAt60FpsWithSilhouettesTests
{
    private const float MinFps            = 55f;
    private const float MeanFps           = 58f;
    private const float P99Fps            = 50f;
    private const float TestDurationSecs  = 60f;
    private const float WarmUpSecs        = 3f;
    private const int   ExpectedNpcCount  = 15;    // office-starter spawns ≥15 NPCs

    private GameObject            _hostGo;
    private EngineHost            _host;
    private FrameRateMonitor      _monitor;
    private NpcSilhouetteRenderer _silhouetteRenderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Boot engine with office-starter.
        _hostGo = new GameObject("PerfGateSil_EngineHost");
        _host   = _hostGo.AddComponent<EngineHost>();

        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        SetField(_host, "_configAsset", configAsset);
        SetField(_host, "_worldDefinitionPath", "office-starter.json");

        // Add renderers — includes full rendering overhead.
        var rendererGo = new GameObject("PerfGateSil_Renderers");
        var roomRend   = rendererGo.AddComponent<RoomRectangleRenderer>();
        SetField(roomRend, "_engineHost", _host);

        // Use the silhouette renderer instead of the dot renderer.
        _silhouetteRenderer = rendererGo.AddComponent<NpcSilhouetteRenderer>();
        SetField(_silhouetteRenderer, "_engineHost", _host);

        // No catalog → null sprites, but renderer gracefully handles null.
        // Full cost: GameObjects + SpriteRenderers + Animators still created.

        // FPS monitor.
        _monitor = new GameObject("PerfGateSil_FPSMonitor").AddComponent<FrameRateMonitor>();

        yield return null;  // Start()
    }

    [TearDown]
    public void TearDown()
    {
        if (_hostGo != null) Object.Destroy(_hostGo);
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            if (go.name.StartsWith("PerfGateSil_"))
                Object.Destroy(go);
        }
    }

    // ── Test ───────────────────────────────────────────────────────────────────

    [UnityTest]
    [Timeout(90000)]   // 90-second UTF timeout
    public IEnumerator ThirtyNpcs_WithSilhouettes_SustainedSixtyFps_For60Seconds()
    {
        Assert.IsNotNull(_host.Engine, "Engine must boot before performance gate.");

        int entityCount = _host.Engine.Entities.Count;
        Assert.GreaterOrEqual(entityCount, ExpectedNpcCount,
            $"office-starter should spawn at least {ExpectedNpcCount} entities; got {entityCount}.");

        // Warm-up: let JIT settle and silhouette instances initialise.
        yield return new WaitForSecondsRealtime(WarmUpSecs);

        _monitor.ResetSamples();

        // 60-second measured run.
        yield return new WaitForSecondsRealtime(TestDurationSecs);

        float min   = _monitor.SampleMin();
        float mean  = _monitor.SampleMean();
        float p99   = _monitor.SampleP99();
        int   count = _monitor.SecondSamples.Count;

        string summary = $"Samples={count} | min={min:F1} mean={mean:F1} p99={p99:F1} " +
                         $"(thresholds: min>={MinFps} mean>={MeanFps} p99>={P99Fps})";
        Debug.Log($"[PerformanceGate30NpcAt60FpsWithSilhouettesTests] {summary}");

        // AT-10 assertions — DO NOT WEAKEN.
        Assert.GreaterOrEqual(count, 55,
            $"Expected at least 55 FPS samples over 60s; got {count}. {summary}");
        Assert.GreaterOrEqual(min, MinFps,
            $"min FPS {min:F1} < threshold {MinFps}. {summary}");
        Assert.GreaterOrEqual(mean, MeanFps,
            $"mean FPS {mean:F1} < threshold {MeanFps}. {summary}");
        Assert.GreaterOrEqual(p99, P99Fps,
            $"p99 FPS {p99:F1} < threshold {P99Fps}. {summary}");
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private static void SetField(object target, string name, object value)
    {
        var f = target.GetType().GetField(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f?.SetValue(target, value);
    }
}
