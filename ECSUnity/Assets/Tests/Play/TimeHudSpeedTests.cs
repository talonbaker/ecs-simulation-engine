using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-08 (speed): Verify all speed multiplier settings propagate to Time.timeScale.
/// </summary>
[TestFixture]
public class TimeHudSpeedTests
{
    private GameObject    _go;
    private TimeHudPanel  _hud;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go  = new GameObject("TimeSpd_HUD");
        _hud = _go.AddComponent<TimeHudPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("TimeSpd_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Default_TimeScale_IsOne()
    {
        // Fresh panel should not modify Time.timeScale away from the Unity default.
        yield return null;
        Assert.AreEqual(1f, Time.timeScale, 0.001f,
            "Default time scale should be 1x.");
    }

    [UnityTest]
    public IEnumerator SetX1_TimeScaleOne()
    {
        _hud.SetTimeScale(1f);
        yield return null;
        Assert.AreEqual(1f, Time.timeScale, 0.001f,
            "SetTimeScale(1f) should produce Time.timeScale == 1.");
    }

    [UnityTest]
    public IEnumerator SetX4_TimeScaleFour()
    {
        _hud.SetTimeScale(4f);
        yield return null;
        Assert.AreEqual(4f, Time.timeScale, 0.001f,
            "SetTimeScale(4f) should produce Time.timeScale == 4.");
    }

    [UnityTest]
    public IEnumerator SetX16_TimeScaleSixteen()
    {
        _hud.SetTimeScale(16f);
        yield return null;
        Assert.AreEqual(16f, Time.timeScale, 0.001f,
            "SetTimeScale(16f) should produce Time.timeScale == 16.");
    }

    [UnityTest]
    public IEnumerator SetHalfSpeed_TimeScaleHalf()
    {
        _hud.SetTimeScale(0.5f);
        yield return null;
        Assert.AreEqual(0.5f, Time.timeScale, 0.001f,
            "SetTimeScale(0.5f) should produce Time.timeScale == 0.5.");
    }
}
