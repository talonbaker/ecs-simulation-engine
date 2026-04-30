using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-01: B key toggles build mode on and off.
/// </summary>
[TestFixture]
public class BuildModeToggleTests
{
    private GameObject           _go;
    private BuildModeController  _ctrl;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go   = new GameObject("BuildModeToggle_Ctrl");
        _ctrl = _go.AddComponent<BuildModeController>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("BuildModeToggle_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Toggle_DefaultIsOff()
    {
        yield return null;
        Assert.IsFalse(_ctrl.IsBuildMode, "Build mode should be off by default.");
    }

    [UnityTest]
    public IEnumerator Toggle_OneTurnOn()
    {
        _ctrl.TestToggleBuildMode();
        yield return null;
        Assert.IsTrue(_ctrl.IsBuildMode, "After one toggle, build mode should be ON.");
    }

    [UnityTest]
    public IEnumerator Toggle_TwoTurnsOff()
    {
        _ctrl.TestToggleBuildMode();
        yield return null;
        _ctrl.TestToggleBuildMode();
        yield return null;
        Assert.IsFalse(_ctrl.IsBuildMode, "After two toggles, build mode should be OFF.");
    }

    [UnityTest]
    public IEnumerator Toggle_IntentClearedOnExit()
    {
        _ctrl.TestToggleBuildMode();
        yield return null;

        // Simulate starting a placement intent.
        var entry = new PaletteEntry
        {
            Label = "Test Wall",
            TemplateIdString = System.Guid.NewGuid().ToString(),
            Category = PaletteCategory.Structural,
        };
        _ctrl.TestSelectPaletteEntry(entry);
        yield return null;

        Assert.AreEqual(BuildIntentKind.Placing, _ctrl.CurrentIntent.Kind,
            "Intent should be Placing after palette selection.");

        // Exit build mode → intent should clear.
        _ctrl.TestToggleBuildMode();
        yield return null;

        Assert.AreEqual(BuildIntentKind.None, _ctrl.CurrentIntent.Kind,
            "Intent should be cleared when build mode turns off.");
    }
}
