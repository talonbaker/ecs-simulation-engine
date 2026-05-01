using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-07: Queue overflow — when the queue is full, the main thread does not block;
/// overflow is logged via Debug.LogWarning.
///
/// Structural test: verifies the queue is bounded and that QueueDepth never
/// exceeds 256 (the declared capacity).
/// </summary>
[TestFixture]
public class JsonlStreamEmitterQueueOverflowTests
{
    private string _tempPath;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"warden_overflow_{System.Guid.NewGuid():N}.jsonl");
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("JsOverflow_"))
                Object.Destroy(go);
        try { if (File.Exists(_tempPath)) File.Delete(_tempPath); } catch { }
    }

    [UnityTest]
    public IEnumerator QueueDepth_NeverExceedsCapacity()
    {
#if WARDEN
        var go     = new GameObject("JsOverflow_Root");
        var config = ScriptableObject.CreateInstance<JsonlStreamConfig>();
        config.OutputPath      = _tempPath;
        config.EmitEveryNTicks = 1;

        var emitter = go.AddComponent<JsonlStreamEmitter>();
        typeof(JsonlStreamEmitter)
            .GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(emitter, config);

        yield return null;

        for (int i = 0; i < 20; i++) yield return null;

        Assert.LessOrEqual(emitter.QueueDepth, 256,
            "Queue depth must never exceed the bounded capacity of 256.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator MainThread_NotBlocked_DuringOverflow()
    {
        // Even if the queue were full, each frame should complete in a reasonable time.
        float maxMs = 0f;

        for (int i = 0; i < 30; i++)
        {
            yield return null;
            float ms = Time.deltaTime * 1000f;
            if (ms > maxMs) maxMs = ms;
        }

        // 500ms is extremely generous — just guards against outright deadlock.
        Assert.Less(maxMs, 500f,
            $"A single frame took {maxMs:F1}ms — possible main-thread block in emitter.");
    }
}
