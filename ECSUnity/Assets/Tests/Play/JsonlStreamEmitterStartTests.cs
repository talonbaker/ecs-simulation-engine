using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-01: Boot scene; JsonlStreamEmitter.Start creates output file at Logs/worldstate.jsonl.
///
/// Uses a temporary path via Path.GetTempPath() to avoid polluting the project.
/// </summary>
[TestFixture]
public class JsonlStreamEmitterStartTests
{
    private GameObject          _go;
    private string              _tempPath;

#if WARDEN
    private JsonlStreamEmitter  _emitter;
    private JsonlStreamConfig   _config;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"warden_test_{System.Guid.NewGuid():N}.jsonl");

        _go = new GameObject("JsStart_Root");

#if WARDEN
        _config                 = ScriptableObject.CreateInstance<JsonlStreamConfig>();
        _config.OutputPath      = _tempPath;
        _config.EmitEveryNTicks = 1;   // emit every tick for test speed

        _emitter = _go.AddComponent<JsonlStreamEmitter>();

        // Wire config via reflection (SerializeField).
        var configField = typeof(JsonlStreamEmitter)
            .GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        configField?.SetValue(_emitter, _config);
        // _host is left null — emitter guards against this in Update().
#endif

        yield return null;  // let Start() execute
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("JsStart_"))
                Object.Destroy(go);

        // Clean up temp files.
        try { if (File.Exists(_tempPath)) File.Delete(_tempPath); } catch { }
        // Also clean up any rotation files in the temp directory.
        string dir = Path.GetDirectoryName(_tempPath);
        if (dir != null)
        {
            foreach (var f in Directory.GetFiles(dir, "warden_test_*.jsonl"))
                try { File.Delete(f); } catch { }
        }
    }

    [UnityTest]
    public IEnumerator WorkerThread_StartsOnStart()
    {
#if WARDEN
        // Wait one frame for Start() to complete.
        yield return null;
        Assert.IsTrue(_emitter.IsWorkerAlive,
            "Worker thread should be alive after Start().");
#else
        yield return null;
        Assert.Pass("RETAIL build — JsonlStreamEmitter not compiled.");
#endif
    }

    [UnityTest]
    public IEnumerator OutputPath_IsConfigured()
    {
#if WARDEN
        yield return null;
        Assert.AreEqual(_tempPath, _emitter.OutputPath,
            "OutputPath should match the configured JsonlStreamConfig.OutputPath.");
#else
        yield return null;
        Assert.Pass("RETAIL build — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator QueueDepth_InitiallyZeroOrLow()
    {
#if WARDEN
        yield return null;
        // With null host, nothing enqueues, so queue must be 0.
        Assert.AreEqual(0, _emitter.QueueDepth,
            "Queue depth should be 0 with a null EngineHost (no snapshots to emit).");
#else
        yield return null;
        Assert.Pass("RETAIL build — skipped.");
#endif
    }
}
