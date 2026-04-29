using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-08 (creative mode): SkipToMorning is callable in creative mode without exceptions.
/// Pause + speed controls coexist correctly in creative mode.
/// </summary>
[TestFixture]
public class TimeHudCreativeModeTests
{
    private GameObject    _go;
    private TimeHudPanel  _hud;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go  = new GameObject("TimeCreative_HUD");
        _hud = _go.AddComponent<TimeHudPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("TimeCreative_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator SkipToMorning_InCreativeMode_NoException()
    {
        _hud.CreativeMode = true;
        yield return null;

        // SkipToMorning should execute without throwing.
        bool threw = false;
        try { _hud.SkipToMorning(); }
        catch { threw = true; }

        Assert.IsFalse(threw,
            "SkipToMorning() should not throw in creative mode.");
    }

    [UnityTest]
    public IEnumerator SkipToMorning_DefaultMode_NoException()
    {
        // Outside creative mode SkipToMorning is a guarded no-op; must not throw.
        bool threw = false;
        try { _hud.SkipToMorning(); }
        catch { threw = true; }

        yield return null;

        Assert.IsFalse(threw,
            "SkipToMorning() should not throw outside creative mode (silently ignored).");
    }

    [UnityTest]
    public IEnumerator PauseInCreativeMode_Works()
    {
        _hud.CreativeMode = true;
        _hud.SetPaused(true);
        yield return null;

        Assert.IsTrue(_hud.IsPaused,
            "Pause should work correctly in creative mode.");
    }

    [UnityTest]
    public IEnumerator CreativeMode_XSpeedAndPauseCoexist()
    {
        // Set x4, pause, unpause — time scale should restore to x4.
        _hud.CreativeMode = true;
        _hud.SetTimeScale(4f);
        yield return null;

        _hud.SetPaused(true);
        yield return null;
        Assert.IsTrue(_hud.IsPaused);

        _hud.SetPaused(false);
        yield return null;

        Assert.AreEqual(4f, Time.timeScale, 0.001f,
            "After resuming from pause in creative mode, time scale should restore to x4.");
    }
}
