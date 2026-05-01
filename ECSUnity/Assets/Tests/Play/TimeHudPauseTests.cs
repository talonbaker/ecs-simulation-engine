using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-08: Time HUD pause halts engine ticking; x4 increases tick rate.
/// </summary>
[TestFixture]
public class TimeHudPauseTests
{
    private GameObject    _go;
    private TimeHudPanel  _hud;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go  = new GameObject("TimeHudPause_HUD");
        _hud = _go.AddComponent<TimeHudPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        // Ensure time scale is restored even if test fails.
        Time.timeScale = 1f;
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("TimeHudPause_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Pause_SetsTimeScaleZero()
    {
        _hud.SetPaused(true);
        yield return null;
        Assert.AreEqual(0f, Time.timeScale, 0.001f,
            "Pausing should set Time.timeScale to 0.");
        Assert.IsTrue(_hud.IsPaused, "IsPaused should be true.");
    }

    [UnityTest]
    public IEnumerator Resume_RestoresTimeScale()
    {
        _hud.SetTimeScale(1f);
        _hud.SetPaused(true);
        yield return null;
        _hud.SetPaused(false);
        yield return null;
        Assert.AreEqual(1f, Time.timeScale, 0.001f,
            "Resuming should restore Time.timeScale to the previous value.");
        Assert.IsFalse(_hud.IsPaused, "IsPaused should be false after resume.");
    }

    [UnityTest]
    public IEnumerator X4_SetsTimeScaleFour()
    {
        _hud.SetTimeScale(4f);
        yield return null;
        Assert.AreEqual(4f, Time.timeScale, 0.001f,
            "x4 speed should set Time.timeScale to 4.");
    }

    [UnityTest]
    public IEnumerator X16_SetsTimeScaleSixteen()
    {
        _hud.SetTimeScale(16f);
        yield return null;
        Assert.AreEqual(16f, Time.timeScale, 0.001f,
            "x16 speed should set Time.timeScale to 16.");
    }

    [UnityTest]
    public IEnumerator PauseWhileX4_ResumeRestoresX4()
    {
        _hud.SetTimeScale(4f);
        yield return null;
        _hud.SetPaused(true);
        yield return null;
        _hud.SetPaused(false);
        yield return null;
        Assert.AreEqual(4f, Time.timeScale, 0.001f,
            "Resuming after pause should restore the x4 time scale.");
    }
}
