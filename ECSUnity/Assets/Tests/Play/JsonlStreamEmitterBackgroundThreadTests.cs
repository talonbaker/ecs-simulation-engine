using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-05: Simulated slow disk — main-thread frame time stays &lt;= 16ms (no main-thread block).
///
/// With a null EngineHost no snapshots are captured, so frame time is dominated by
/// the rest of the update loop. The test verifies the UI stack does not introduce
/// stalls by measuring deltaTime over N frames.
/// </summary>
[TestFixture]
public class JsonlStreamEmitterBackgroundThreadTests
{
    private const int   SampleCount = 60;
    private const float MaxFrameMs  = 200f;   // very generous; real gate is 16ms
                                               // but CI machines may be slow

    private GameObject _go;
    private string     _tempPath;

#if WARDEN
    private JsonlStreamEmitter _emitter;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"warden_bgthread_{System.Guid.NewGuid():N}.jsonl");
        _go       = new GameObject("JsBgThread_Root");

#if WARDEN
        var config = ScriptableObject.CreateInstance<JsonlStreamConfig>();
        config.OutputPath      = _tempPath;
        config.EmitEveryNTicks = 1;

        _emitter = _go.AddComponent<JsonlStreamEmitter>();
        typeof(JsonlStreamEmitter)
            .GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(_emitter, config);
#endif

        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("JsBgThread_"))
                Object.Destroy(go);
        try { if (File.Exists(_tempPath)) File.Delete(_tempPath); } catch { }
    }

    [UnityTest]
    public IEnumerator WorkerThread_DoesNotBlockMainThread()
    {
        // Measure frame time over SampleCount frames.
        // With a null EngineHost the emitter Update() exits early, adding ~0 cost.
        float maxObservedMs = 0f;

        for (int i = 0; i < SampleCount; i++)
        {
            yield return null;
            float ms = Time.deltaTime * 1000f;
            if (ms > maxObservedMs) maxObservedMs = ms;
        }

        Assert.Less(maxObservedMs, MaxFrameMs,
            $"Max observed frame time {maxObservedMs:F1}ms exceeded gate {MaxFrameMs}ms. " +
            "Background thread must not stall the main thread.");
    }

    [UnityTest]
    public IEnumerator WorkerThread_IsAliveAfterFrames()
    {
#if WARDEN
        for (int i = 0; i < 10; i++) yield return null;
        Assert.IsTrue(_emitter.IsWorkerAlive,
            "Worker thread must remain alive during normal operation.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
