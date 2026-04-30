using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>AT-07: force-kill command — structural error tests.</summary>
[TestFixture]
public class DevConsoleForceKillTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel _panel;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_ForceKill");
#if WARDEN
        _panel = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_ForceKill"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator ForceKill_NoEngine_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("force-kill donna Choked");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "force-kill without engine must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator ForceKill_InvalidCause_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("force-kill donna BogusDeathCause");
        yield return null;

        // Either entity not found (no engine) or invalid cause — either way an error.
        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "force-kill with invalid cause must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
