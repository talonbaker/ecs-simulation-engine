using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-08 (skip-morning): SkipToMorning button hidden outside creative mode.
/// </summary>
[TestFixture]
public class TimeHudNoSkipMorningTests
{
    private GameObject    _go;
    private TimeHudPanel  _hud;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go  = new GameObject("TimeNoSkip_HUD");
        _hud = _go.AddComponent<TimeHudPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("TimeNoSkip_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Default_SkipMorningNotVisible()
    {
        // By default creative mode is off so the skip-morning button must be hidden.
        yield return null;
        Assert.IsFalse(_hud.IsSkipMorningVisible,
            "Skip-to-morning button should be hidden when creative mode is off.");
    }

    [UnityTest]
    public IEnumerator NonCreativeMode_SkipMorningHidden()
    {
        _hud.CreativeMode = false;
        yield return null;
        Assert.IsFalse(_hud.IsSkipMorningVisible,
            "Skip-to-morning should remain hidden with CreativeMode=false.");
    }

    [UnityTest]
    public IEnumerator CreativeMode_SkipMorningVisible()
    {
        _hud.CreativeMode = true;
        yield return null;
        Assert.IsTrue(_hud.IsSkipMorningVisible,
            "Skip-to-morning should be visible when CreativeMode=true.");
    }

    [UnityTest]
    public IEnumerator ToggleCreativeMode_TogglesSkipVisibility()
    {
        // Enable then disable — visibility must follow.
        _hud.CreativeMode = true;
        yield return null;
        Assert.IsTrue(_hud.IsSkipMorningVisible);

        _hud.CreativeMode = false;
        yield return null;
        Assert.IsFalse(_hud.IsSkipMorningVisible,
            "Disabling creative mode should hide the skip-morning button.");
    }
}
