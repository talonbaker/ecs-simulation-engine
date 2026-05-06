using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Play-mode tests for 'scenario throw' — WP-PT.1.</summary>
[TestFixture]
public class DevConsoleScenarioThrowTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel _panel;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_ScenarioThrow");
#if WARDEN
        _panel = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_ScenarioThrow"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Throw_NoEngine_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("scenario throw banana at donna");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }
        Assert.IsTrue(hasError, "scenario throw without engine must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Throw_MissingAtKeyword_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("scenario throw banana toward donna");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }
        Assert.IsTrue(hasError, "scenario throw without 'at' keyword must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Throw_InsufficientArgs_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("scenario throw banana");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }
        Assert.IsTrue(hasError, "scenario throw with insufficient args must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
