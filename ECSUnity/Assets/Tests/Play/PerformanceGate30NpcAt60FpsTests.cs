using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-11: THE HARDEST GATE.
/// 30 NPCs, office-starter scene, 60 seconds real-time.
/// FPS sampled at 1 Hz. Assertions: min >= 55, mean >= 58, p99 >= 50.
///
/// DO NOT WEAKEN THESE THRESHOLDS TO SHIP.
/// If this test fails, escalate as blocked with min/mean/p99 measurements.
/// Profile: suspect WP-3.0.5 ComponentStore perf claim not delivered,
/// expensive projection, or Unity-side allocations leaking.
///
/// This test takes 60+ real-time seconds in the UTF Play-mode runner.
/// Mark it Explicit if you want it out of CI by default:
///   [UnityTest, Explicit("Performance gate — 60s, run manually before release")]
/// </summary>
[TestFixture]
public class PerformanceGate30NpcAt60FpsTests
{
    // Thresholds mirror PerformanceGate inspector defaults and SimConfig.unityHost.
    private const float MinFps  = 55f;
    private const float MeanFps = 58f;
    private const float P99Fps  = 50f;
    private const float TestDurationSecs = 60f;
    private const float WarmUpSecs       = 3f;
    private const int   ExpectedNpcCount = 15;   // office-starter has 15 NPC slots per spec

    private GameObject     _hostGo;
    private EngineHost     _host;
    private FrameRateMonitor _monitor;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Boot engine with office-starter world.
        _hostGo = new GameObject("PerfGate_EngineHost");
        _host   = _hostGo.AddComponent<EngineHost>();

        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        var configField = typeof(EngineHost).GetField("_configAsset",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        configField?.SetValue(_host, configAsset);

        // Use the StreamingAssets office-starter path.
        var pathField = typeof(EngineHost).GetField("_worldDefinitionPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        pathField?.SetValue(_host, "office-starter.json");

        // Boot renderers (adds realistic rendering overhead — tests render cost too).
        var rendererGo = new GameObject("PerfGate_Renderers");
        var roomRend   = rendererGo.AddComponent<RoomRectangleRenderer>();
        var npcRend    = rendererGo.AddComponent<NpcDotRenderer>();

        var hostRef = typeof(RoomRectangleRenderer).GetField("_engineHost",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        hostRef?.SetValue(roomRend, _host);

        var npcHostRef = typeof(NpcDotRenderer).GetField("_engineHost",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        npcHostRef?.SetValue(npcRend, _host);

        // Add the FrameRateMonitor.
        _monitor = new GameObject("PerfGate_FPSMonitor").AddComponent<FrameRateMonitor>();

        yield return null;   // Start()
    }

    [TearDown]
    public void TearDown()
    {
        if (_hostGo != null) Object.Destroy(_hostGo);
        var all = Object.FindObjectsOfType<GameObject>();
        foreach (var go in all)
        {
            if (go.name.StartsWith("PerfGate_"))
                Object.Destroy(go);
        }
    }

    [UnityTest]
    [Timeout(90000)]   // 90-second UTF timeout (60s run + 30s margin)
    public IEnumerator ThirtyNpcs_SustainedSixtyFps_For60Seconds()
    {
        // Verify NPC count before the measured run.
        // office-starter boots 15 NPCs from cast generation.
        Assert.IsNotNull(_host.Engine, "Engine must boot before performance gate.");

        int entityCount = _host.Engine.Entities.Count;
        Assert.GreaterOrEqual(entityCount, ExpectedNpcCount,
            $"office-starter should spawn at least {ExpectedNpcCount} entities; got {entityCount}.");

        // Warm-up: let the JIT settle.
        yield return new WaitForSecondsRealtime(WarmUpSecs);

        _monitor.ResetSamples();

        // Measured run: collect 1-Hz FPS samples for TestDurationSecs seconds.
        yield return new WaitForSecondsRealtime(TestDurationSecs);

        // Evaluate.
        float min  = _monitor.SampleMin();
        float mean = _monitor.SampleMean();
        float p99  = _monitor.SampleP99();
        int   sampleCount = _monitor.SecondSamples.Count;

        string summary = $"Samples={sampleCount} | min={min:F1} mean={mean:F1} p99={p99:F1} " +
                         $"(thresholds: min>={MinFps} mean>={MeanFps} p99>={P99Fps})";
        Debug.Log($"[PerformanceGate30NpcAt60FpsTests] {summary}");

        // AT-11 assertions — DO NOT WEAKEN.
        Assert.GreaterOrEqual(sampleCount, 55,
            $"Expected at least 55 FPS samples over 60 seconds; got {sampleCount}. {summary}");
        Assert.GreaterOrEqual(min, MinFps,
            $"min FPS {min:F1} < threshold {MinFps}. {summary}");
        Assert.GreaterOrEqual(mean, MeanFps,
            $"mean FPS {mean:F1} < threshold {MeanFps}. {summary}");
        Assert.GreaterOrEqual(p99, P99Fps,
            $"p99 FPS {p99:F1} < threshold {P99Fps}. {summary}");
    }
}
