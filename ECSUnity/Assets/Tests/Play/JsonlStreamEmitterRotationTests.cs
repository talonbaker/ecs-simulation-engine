using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-06: File rotation — when bytes written exceed RotationSizeBytes, the old
/// file is renamed and a fresh file starts.
///
/// This test exercises the per-session rotation that happens at Start() when
/// a prior file exists.
/// </summary>
[TestFixture]
public class JsonlStreamEmitterRotationTests
{
    private string _tempPath;
    private string _tempDir;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _tempDir  = Path.Combine(Path.GetTempPath(), $"warden_rot_{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tempPath = Path.Combine(_tempDir, "worldstate.jsonl");
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("JsRotation_"))
                Object.Destroy(go);

        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    [UnityTest]
    public IEnumerator SessionStart_PreexistingFile_Rotated()
    {
#if WARDEN
        // Create a pre-existing "prior session" file.
        File.WriteAllText(_tempPath, "{\"prior\":\"session\"}\n");
        Assert.IsTrue(File.Exists(_tempPath), "Pre-condition: prior session file must exist.");

        // Spawn the emitter — its Start() should rotate the prior file.
        var go     = new GameObject("JsRotation_Root");
        var config = ScriptableObject.CreateInstance<JsonlStreamConfig>();
        config.OutputPath      = _tempPath;
        config.EmitEveryNTicks = 999;   // very infrequent so we don't write real data

        var emitter = go.AddComponent<JsonlStreamEmitter>();
        typeof(JsonlStreamEmitter)
            .GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(emitter, config);

        yield return null;  // triggers Start()

        // Check that at least one rotated file exists in the temp dir.
        var files = Directory.GetFiles(_tempDir, "worldstate.*.jsonl");
        Assert.IsTrue(files.Length > 0,
            "A prior-session file must be rotated (renamed with timestamp) on Start().");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Config_SmallRotationSize_IsAccepted()
    {
#if WARDEN
        // Verify that a very small rotation threshold (1 KB) is accepted without error.
        var go     = new GameObject("JsRotation_Small");
        var config = ScriptableObject.CreateInstance<JsonlStreamConfig>();
        config.OutputPath        = _tempPath;
        config.RotationSizeBytes = 1024;  // 1 KB — rotate after 1 KB
        config.EmitEveryNTicks   = 999;

        var emitter = go.AddComponent<JsonlStreamEmitter>();
        typeof(JsonlStreamEmitter)
            .GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(emitter, config);

        yield return null;

        Assert.IsTrue(emitter.IsWorkerAlive,
            "Worker thread must start even with a very small rotation threshold.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
