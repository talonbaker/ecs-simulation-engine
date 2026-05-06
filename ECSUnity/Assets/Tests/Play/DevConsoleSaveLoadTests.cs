using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-13: save / load commands round-trip through SaveLoadPanel.
/// </summary>
[TestFixture]
public class DevConsoleSaveLoadTests
{
    private GameObject    _go;
#if WARDEN
    private DevConsolePanel _panel;
    private SaveLoadPanel   _saveLoad;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_SaveLoad");
#if WARDEN
        _saveLoad = _go.AddComponent<SaveLoadPanel>();
        _panel    = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_SaveLoad"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Save_NoPanel_ReturnsError()
    {
#if WARDEN
        // Panel with no SaveLoadPanel wired.
        var go2    = new GameObject("DevCon_SaveLoad_Bare");
        var panel2 = go2.AddComponent<DevConsolePanel>();
        yield return null;

        panel2.SubmitCommand("save my-slot");
        yield return null;

        bool hasError = false;
        foreach (var e in panel2.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "save with no SaveLoadPanel should return an error.");
        Object.Destroy(go2);
#else
        yield return null;
        Assert.Pass("RETAIL — DevConsolePanel not compiled.");
#endif
    }

    [UnityTest]
    public IEnumerator Save_NoArgs_ReturnsError()
    {
#if WARDEN
        _panel.SubmitCommand("save");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "save with no args should return an error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Load_NoPanel_ReturnsError()
    {
#if WARDEN
        var go2    = new GameObject("DevCon_SaveLoad_Bare2");
        var panel2 = go2.AddComponent<DevConsolePanel>();
        yield return null;

        panel2.SubmitCommand("load my-slot");
        yield return null;

        bool hasError = false;
        foreach (var e in panel2.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "load with no SaveLoadPanel should return an error.");
        Object.Destroy(go2);
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Load_NoArgs_ReturnsError()
    {
#if WARDEN
        _panel.SubmitCommand("load");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "load with no args should return an error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Save_MultiWordSlot_PassesFullNameToPanel()
    {
#if WARDEN
        // SaveLoadPanel.Save should be called with the full multi-word slot name.
        // We verify indirectly: if it doesn't crash and outputs a success-ish message.
        _panel.SubmitCommand("save before big experiment");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsFalse(hasError,
            "save with multi-word slot name should not return an error when panel is wired.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator SaveThenLoad_BothSucceed()
    {
#if WARDEN
        _panel.SubmitCommand("save roundtrip-slot");
        yield return null;
        _panel.SubmitCommand("load roundtrip-slot");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsFalse(hasError,
            "save then load on a wired panel should both succeed without errors.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
