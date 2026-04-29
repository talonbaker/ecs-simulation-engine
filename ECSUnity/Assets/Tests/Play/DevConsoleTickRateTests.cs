using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-11: tick-rate command adjusts Time.fixedDeltaTime.
/// </summary>
[TestFixture]
public class DevConsoleTickRateTests
{
    private GameObject _go;
    private float      _originalFixedDeltaTime;
#if WARDEN
    private DevConsolePanel _panel;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _originalFixedDeltaTime = Time.fixedDeltaTime;
        _go = new GameObject("DevCon_TickRate");
#if WARDEN
        _panel = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        // Always restore fixedDeltaTime so subsequent tests are unaffected.
        Time.fixedDeltaTime = _originalFixedDeltaTime;
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_TickRate"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator TickRate50_SetsFixedDeltaTime()
    {
#if WARDEN
        _panel.SubmitCommand("tick-rate 50");
        yield return null;

        Assert.AreEqual(1f / 50f, Time.fixedDeltaTime, 0.0001f,
            "tick-rate 50 should set fixedDeltaTime to 0.02.");
#else
        yield return null;
        Assert.Pass("RETAIL — DevConsolePanel not compiled.");
#endif
    }

    [UnityTest]
    public IEnumerator TickRate1_MinimumAllowed()
    {
#if WARDEN
        _panel.SubmitCommand("tick-rate 1");
        yield return null;

        Assert.AreEqual(1f / 1f, Time.fixedDeltaTime, 0.0001f,
            "tick-rate 1 should set fixedDeltaTime to 1.0.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator TickRate200_FastForward()
    {
#if WARDEN
        _panel.SubmitCommand("tick-rate 200");
        yield return null;

        Assert.AreEqual(1f / 200f, Time.fixedDeltaTime, 0.00001f,
            "tick-rate 200 should set fixedDeltaTime to 0.005.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator TickRateAlias_WorksWithTickrate()
    {
#if WARDEN
        _panel.SubmitCommand("tickrate 100");
        yield return null;

        Assert.AreEqual(1f / 100f, Time.fixedDeltaTime, 0.00001f,
            "'tickrate' alias should work identically to 'tick-rate'.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator TickRateZero_ReturnsError()
    {
#if WARDEN
        float before = Time.fixedDeltaTime;
        _panel.SubmitCommand("tick-rate 0");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "tick-rate 0 should produce an error.");
        Assert.AreEqual(before, Time.fixedDeltaTime, 0.0001f,
            "fixedDeltaTime must not change on invalid input.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator TickRateNoArgs_ReturnsError()
    {
#if WARDEN
        _panel.SubmitCommand("tick-rate");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "tick-rate with no args should produce an error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator TickRateAboveMax_ClampedTo1000()
    {
#if WARDEN
        _panel.SubmitCommand("tick-rate 9999");
        yield return null;

        // Should be clamped to 1000 tps.
        Assert.AreEqual(1f / 1000f, Time.fixedDeltaTime, 0.000001f,
            "tick-rate above 1000 should be clamped to 1000 tps.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
