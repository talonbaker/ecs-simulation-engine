using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-10: Settings soften-mode toggle persists across frames.
/// </summary>
[TestFixture]
public class SettingsSoftenToggleTests
{
    private GameObject     _go;
    private SettingsPanel  _panel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go    = new GameObject("SetSoften_Panel");
        _panel = _go.AddComponent<SettingsPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("SetSoften_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Default_SoftenDisabled()
    {
        yield return null;
        Assert.IsFalse(_panel.SoftenModeEnabled,
            "Soften mode should be disabled by default.");
    }

    [UnityTest]
    public IEnumerator EnableSoften_IsEnabled()
    {
        _panel.SetSoftenMode(true);
        yield return null;
        Assert.IsTrue(_panel.SoftenModeEnabled,
            "SoftenModeEnabled should be true after SetSoftenMode(true).");
    }

    [UnityTest]
    public IEnumerator DisableSoften_IsDisabled()
    {
        _panel.SetSoftenMode(true);
        yield return null;
        _panel.SetSoftenMode(false);
        yield return null;
        Assert.IsFalse(_panel.SoftenModeEnabled,
            "SoftenModeEnabled should be false after SetSoftenMode(false).");
    }

    [UnityTest]
    public IEnumerator ToggleSoftenTwice_ReturnsFalse()
    {
        _panel.SetSoftenMode(true);
        _panel.SetSoftenMode(false);
        yield return null;
        Assert.IsFalse(_panel.SoftenModeEnabled,
            "After two toggles (on then off) soften mode should be false.");
    }
}
