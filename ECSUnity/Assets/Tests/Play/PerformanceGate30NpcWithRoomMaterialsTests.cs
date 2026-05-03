using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-10: 30 NPCs in PlaytestScene with room identity materials hold ≥ 60 FPS.
///
/// Extends the existing PerformanceGate30NpcAt60FpsTests with the
/// RoomVisualIdentityLoader wired into RoomRectangleRenderer.
///
/// Same thresholds: min ≥ 55, mean ≥ 58, p99 ≥ 50 over 60 seconds.
/// If this test fails and the baseline PerformanceGate30NpcAt60FpsTests passes,
/// the regression is caused by room material overhead.
/// </summary>
[TestFixture]
public class PerformanceGate30NpcWithRoomMaterialsTests
{
    private const float MinFps          = 55f;
    private const float MeanFps         = 58f;
    private const float P99Fps          = 50f;
    private const float TestDurationSecs = 60f;
    private const float WarmUpSecs       = 3f;

    private GameObject        _hostGo;
    private EngineHost        _host;
    private FrameRateMonitor  _monitor;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _hostGo = new GameObject("PerfMat_EngineHost");
        _host   = _hostGo.AddComponent<EngineHost>();

        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        var configField = typeof(EngineHost).GetField("_configAsset",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        configField?.SetValue(_host, configAsset);

        var pathField = typeof(EngineHost).GetField("_worldDefinitionPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        pathField?.SetValue(_host, "office-starter.json");

        // Wire renderers with visual identity loader.
        var rendererGo = new GameObject("PerfMat_Renderers");
        var roomRend   = rendererGo.AddComponent<RoomRectangleRenderer>();
        var npcRend    = rendererGo.AddComponent<NpcDotRenderer>();
        var loader     = rendererGo.AddComponent<RoomVisualIdentityLoader>();

        var hostRef = typeof(RoomRectangleRenderer).GetField("_engineHost",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        hostRef?.SetValue(roomRend, _host);

        var loaderRef = typeof(RoomRectangleRenderer).GetField("_visualIdentityLoader",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        loaderRef?.SetValue(roomRend, loader);

        var npcHostRef = typeof(NpcDotRenderer).GetField("_engineHost",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        npcHostRef?.SetValue(npcRend, _host);

        _monitor = new GameObject("PerfMat_FPSMonitor").AddComponent<FrameRateMonitor>();

        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        if (_hostGo != null) Object.Destroy(_hostGo);
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("PerfMat_"))
                Object.Destroy(go);
    }

    [UnityTest]
    [Timeout(90000)]
    public IEnumerator ThirtyNpcs_WithRoomMaterials_SustainSixtyFps()
    {
        Assert.IsNotNull(_host.Engine, "Engine must boot before performance gate.");

        yield return new WaitForSecondsRealtime(WarmUpSecs);
        _monitor.ResetSamples();

        yield return new WaitForSecondsRealtime(TestDurationSecs);

        float min  = _monitor.SampleMin();
        float mean = _monitor.SampleMean();
        float p99  = _monitor.SampleP99();
        int   n    = _monitor.SecondSamples.Count;

        string summary = $"Samples={n} min={min:F1} mean={mean:F1} p99={p99:F1} " +
                         $"(thresholds: min>={MinFps} mean>={MeanFps} p99>={P99Fps})";
        UnityEngine.Debug.Log($"[PerfMatGate] {summary}");

        Assert.GreaterOrEqual(n, 55, $"Expected ≥55 FPS samples; got {n}. {summary}");
        Assert.GreaterOrEqual(min,  MinFps,  $"min FPS {min:F1} < {MinFps}. {summary}");
        Assert.GreaterOrEqual(mean, MeanFps, $"mean FPS {mean:F1} < {MeanFps}. {summary}");
        Assert.GreaterOrEqual(p99,  P99Fps,  $"p99 FPS {p99:F1} < {P99Fps}. {summary}");
    }
}
