using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Play-mode tests for 'scenario lockout' — WP-PT.1.</summary>
[TestFixture]
public class DevConsoleScenarioLockoutTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel _panel;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_ScenarioLockout");
#if WARDEN
        _panel = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_ScenarioLockout"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Lockout_NoEngine_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("scenario lockout donna");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }
        Assert.IsTrue(hasError, "scenario lockout without engine must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Lockout_NoArgs_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("scenario lockout");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }
        Assert.IsTrue(hasError, "scenario lockout with no args must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
