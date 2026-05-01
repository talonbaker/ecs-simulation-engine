using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-10: Creative mode toggle is independent of soften mode.
/// </summary>
[TestFixture]
public class SettingsCreativeModeToggleTests
{
    private GameObject     _go;
    private SettingsPanel  _panel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go    = new GameObject("SetCreative_Panel");
        _panel = _go.AddComponent<SettingsPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("SetCreative_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Default_CreativeDisabled()
    {
        yield return null;
        Assert.IsFalse(_panel.CreativeModeEnabled,
            "Creative mode should be disabled by default.");
    }

    [UnityTest]
    public IEnumerator Enable_IsEnabled()
    {
        _panel.SetCreativeMode(true);
        yield return null;
        Assert.IsTrue(_panel.CreativeModeEnabled,
            "CreativeModeEnabled should be true after SetCreativeMode(true).");
    }

    [UnityTest]
    public IEnumerator Disable_IsDisabled()
    {
        _panel.SetCreativeMode(true);
        yield return null;
        _panel.SetCreativeMode(false);
        yield return null;
        Assert.IsFalse(_panel.CreativeModeEnabled,
            "CreativeModeEnabled should be false after SetCreativeMode(false).");
    }

    [UnityTest]
    public IEnumerator Toggle_DoesNotAffectSoften()
    {
        // Creative mode and soften mode are independent flags.
        _panel.SetCreativeMode(true);
        yield return null;
        Assert.IsFalse(_panel.SoftenModeEnabled,
            "Enabling creative mode must not enable soften mode.");
    }
}
