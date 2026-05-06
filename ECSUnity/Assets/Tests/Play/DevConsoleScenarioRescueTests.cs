using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Play-mode tests for 'scenario rescue' — WP-PT.1.</summary>
[TestFixture]
public class DevConsoleScenarioRescueTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel _panel;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_ScenarioRescue");
#if WARDEN
        _panel = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_ScenarioRescue"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Rescue_NoEngine_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("scenario rescue donna");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }
        Assert.IsTrue(hasError, "scenario rescue without engine must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Rescue_NoArgs_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("scenario rescue");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }
        Assert.IsTrue(hasError, "scenario rescue with no args must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
