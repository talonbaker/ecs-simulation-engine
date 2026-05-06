using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Play-mode tests for 'scenario sound' — WP-PT.1.</summary>
[TestFixture]
public class DevConsoleScenarioSoundTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel _panel;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_ScenarioSound");
#if WARDEN
        _panel = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_ScenarioSound"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Sound_NoEngine_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("scenario sound Cough");
        yield return null;

        // Without engine SoundBus is unavailable, so error expected.
        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }
        Assert.IsTrue(hasError, "scenario sound without SoundBus must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Sound_InvalidKind_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("scenario sound BogusNoise");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }
        Assert.IsTrue(hasError, "scenario sound with invalid kind must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Sound_NoArgs_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("scenario sound");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }
        Assert.IsTrue(hasError, "scenario sound with no args must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
