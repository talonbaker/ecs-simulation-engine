using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-02: Build palette is visible only when build mode is active.
/// </summary>
[TestFixture]
public class BuildPaletteVisibilityTests
{
    private GameObject       _ctrlGo;
    private BuildModeController _ctrl;
    private GameObject       _paletteGo;
    private BuildPaletteUI   _palette;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _ctrlGo   = new GameObject("BPVis_Ctrl");
        _ctrl     = _ctrlGo.AddComponent<BuildModeController>();

        _paletteGo = new GameObject("BPVis_Palette");
        _palette   = _paletteGo.AddComponent<BuildPaletteUI>();

        // Wire palette to controller via reflection.
        var field = typeof(BuildModeController).GetField("_palette",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_ctrl, _palette);

        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("BPVis_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Palette_HiddenByDefault()
    {
        yield return null;
        Assert.IsFalse(_palette.IsVisible, "Palette should be hidden when build mode is OFF.");
    }

    [UnityTest]
    public IEnumerator Palette_ShownWhenBuildModeOn()
    {
        _ctrl.TestToggleBuildMode();
        yield return null;
        Assert.IsTrue(_palette.IsVisible, "Palette should be visible when build mode is ON.");
    }

    [UnityTest]
    public IEnumerator Palette_HiddenWhenBuildModeOff()
    {
        _ctrl.TestToggleBuildMode();
        yield return null;
        _ctrl.TestToggleBuildMode();
        yield return null;
        Assert.IsFalse(_palette.IsVisible, "Palette should be hidden after build mode toggles back OFF.");
    }
}
