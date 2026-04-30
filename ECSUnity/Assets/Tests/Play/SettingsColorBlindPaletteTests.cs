using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-10: Color-blind palette selection cycles through all four options.
/// </summary>
[TestFixture]
public class SettingsColorBlindPaletteTests
{
    private GameObject     _go;
    private SettingsPanel  _panel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go    = new GameObject("SetCB_Panel");
        _panel = _go.AddComponent<SettingsPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("SetCB_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Default_PaletteIsDefault()
    {
        yield return null;
        Assert.AreEqual(ColorBlindPalette.Default, _panel.CurrentColorBlindPalette,
            "Default color-blind palette should be Default.");
    }

    [UnityTest]
    public IEnumerator SetDeuteranopia_PaletteChanges()
    {
        _panel.SetColorBlindPalette(ColorBlindPalette.Deuteranopia);
        yield return null;
        Assert.AreEqual(ColorBlindPalette.Deuteranopia, _panel.CurrentColorBlindPalette,
            "Palette should switch to Deuteranopia.");
    }

    [UnityTest]
    public IEnumerator SetProtanopia_PaletteChanges()
    {
        _panel.SetColorBlindPalette(ColorBlindPalette.Protanopia);
        yield return null;
        Assert.AreEqual(ColorBlindPalette.Protanopia, _panel.CurrentColorBlindPalette,
            "Palette should switch to Protanopia.");
    }

    [UnityTest]
    public IEnumerator SetTritanopia_PaletteChanges()
    {
        _panel.SetColorBlindPalette(ColorBlindPalette.Tritanopia);
        yield return null;
        Assert.AreEqual(ColorBlindPalette.Tritanopia, _panel.CurrentColorBlindPalette,
            "Palette should switch to Tritanopia.");
    }
}
