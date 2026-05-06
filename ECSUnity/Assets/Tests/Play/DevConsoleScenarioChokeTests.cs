using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Play-mode tests for 'scenario choke' — WP-PT.1.</summary>
[TestFixture]
public class DevConsoleScenarioChokeTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel _panel;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_ScenarioChoke");
#if WARDEN
        _panel = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_ScenarioChoke"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Choke_NoEngine_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("scenario choke donna");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }
        Assert.IsTrue(hasError, "scenario choke without engine must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Choke_NoArgs_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("scenario choke");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }
        Assert.IsTrue(hasError, "scenario choke with no args must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Choke_InvalidBolusSize_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("scenario choke donna --bolus-size enormous");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }
        Assert.IsTrue(hasError, "scenario choke with invalid bolus size must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
