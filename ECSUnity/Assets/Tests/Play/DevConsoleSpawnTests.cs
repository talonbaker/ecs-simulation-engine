using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-04: spawn command creates a new entity.
/// Requires a live EngineHost; structural test checks for error/success output.
/// </summary>
[TestFixture]
public class DevConsoleSpawnTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel _panel;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_Spawn");
#if WARDEN
        _panel = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_Spawn"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Spawn_NoEngine_ReturnsError()
    {
#if WARDEN
        // With null host the spawn command must return an error.
        _panel.Open();
        _panel.SubmitCommand("spawn worker 5 5");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "spawn with null EngineHost must return an error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Spawn_TooFewArgs_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("spawn worker");  // missing x and z
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "spawn with missing args must return an error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
