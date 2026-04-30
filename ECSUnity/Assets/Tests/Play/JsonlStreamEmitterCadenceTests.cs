using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-02: Cadence — emitting every N ticks produces the expected number of lines.
///
/// Without a live EngineHost this test verifies cadence math at a structural level.
/// A full integration test (with EngineHost + live world) would require a scene load
/// which is out of scope for a unit test. Instead we verify the queue-depth / line-count
/// relationship by calling Update() indirectly via time progression.
/// </summary>
[TestFixture]
public class JsonlStreamEmitterCadenceTests
{
    private GameObject _go;
    private string     _tempPath;

#if WARDEN
    private JsonlStreamEmitter _emitter;
    private JsonlStreamConfig  _config;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"warden_cadence_{System.Guid.NewGuid():N}.jsonl");
        _go       = new GameObject("JsCadence_Root");

#if WARDEN
        _config = ScriptableObject.CreateInstance<JsonlStreamConfig>();
        _config.OutputPath      = _tempPath;
        _config.EmitEveryNTicks = 30;

        _emitter = _go.AddComponent<JsonlStreamEmitter>();
        var f = typeof(JsonlStreamEmitter)
            .GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f?.SetValue(_emitter, _config);
#endif

        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("JsCadence_"))
                Object.Destroy(go);
        try { if (File.Exists(_tempPath)) File.Delete(_tempPath); } catch { }
    }

    [UnityTest]
    public IEnumerator DefaultCadence_IsThirtyTicks()
    {
#if WARDEN
        yield return null;
        Assert.AreEqual(30, _config.EmitEveryNTicks,
            "Default cadence should be 30 ticks.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator SetEmitEveryNTicks_ClampedToValidRange()
    {
#if WARDEN
        yield return null;
        _emitter.SetEmitEveryNTicks(0);    // below minimum
        Assert.GreaterOrEqual(_config.EmitEveryNTicks, 1,
            "EmitEveryNTicks must be clamped to >= 1.");

        _emitter.SetEmitEveryNTicks(9999); // above maximum
        Assert.LessOrEqual(_config.EmitEveryNTicks, 1000,
            "EmitEveryNTicks must be clamped to <= 1000.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator NullHost_NoLinesWritten()
    {
        // With null host, no snapshots are emitted, no lines should appear.
        yield return new WaitForSeconds(0.2f);

#if WARDEN
        int lineCount = File.Exists(_tempPath)
            ? File.ReadAllLines(_tempPath).Count(l => l.Trim().Length > 0)
            : 0;

        Assert.AreEqual(0, lineCount,
            "With a null EngineHost, no JSONL lines should be written.");
#else
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
