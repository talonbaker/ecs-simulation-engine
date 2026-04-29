using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-03: World tint overlay is applied only when build mode is active.
/// </summary>
[TestFixture]
public class BuildOverlayTintTests
{
    private GameObject        _ctrlGo;
    private BuildModeController _ctrl;
    private GameObject        _overlayGo;
    private BuildOverlay      _overlay;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _ctrlGo   = new GameObject("BOTint_Ctrl");
        _ctrl     = _ctrlGo.AddComponent<BuildModeController>();

        // Overlay requires an Image component.
        _overlayGo = new GameObject("BOTint_Overlay");
        _overlayGo.AddComponent<Canvas>(); // parent canvas required by Image
        _overlayGo.AddComponent<UnityEngine.UI.Image>();
        _overlay   = _overlayGo.AddComponent<BuildOverlay>();

        // Wire overlay into controller.
        var field = typeof(BuildModeController).GetField("_overlay",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_ctrl, _overlay);

        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("BOTint_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Overlay_HiddenByDefault()
    {
        yield return null;
        Assert.IsFalse(_overlay.IsVisible, "Overlay should be invisible when build mode is OFF.");
    }

    [UnityTest]
    public IEnumerator Overlay_VisibleWhenBuildModeOn()
    {
        _ctrl.TestToggleBuildMode();
        yield return null;
        Assert.IsTrue(_overlay.IsVisible, "Overlay should be visible when build mode is ON.");
    }

    [UnityTest]
    public IEnumerator Overlay_HasPositiveAlphaWhenOn()
    {
        _ctrl.TestToggleBuildMode();
        yield return null;
        Assert.Greater(_overlay.CurrentAlpha, 0f,
            "Overlay alpha should be > 0 when build mode is active.");
    }

    [UnityTest]
    public IEnumerator Overlay_HiddenAfterToggleOff()
    {
        _ctrl.TestToggleBuildMode();
        yield return null;
        _ctrl.TestToggleBuildMode();
        yield return null;
        Assert.IsFalse(_overlay.IsVisible, "Overlay should disappear after build mode turns back OFF.");
    }
}
