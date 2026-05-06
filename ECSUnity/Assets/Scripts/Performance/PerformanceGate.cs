using System.Collections;
using UnityEngine;

/// <summary>
/// Runtime performance gate component.
///
/// Runs a timed measurement and exposes the result for inspection in the Inspector
/// or by play-mode tests (<see cref="PerformanceGate30NpcAt60FpsTests"/>).
///
/// This component does NOT assert — it only measures and logs. Assertions live in
/// the UTF test file so the component compiles cleanly in non-test builds.
///
/// GATE SPECIFICATION
/// ──────────────────
/// Run duration:  60 real-time seconds (configurable in Inspector).
/// Sample rate:   1 Hz (via FrameRateMonitor.SecondSamples).
/// Thresholds (AT-11 — DO NOT WEAKEN):
///   min  >= 55 FPS
///   mean >= 58 FPS
///   p99  >= 50 FPS
///
/// After the run, <see cref="IsFinished"/> is true and <see cref="LastResult"/>
/// contains the exact measurements. <see cref="Passed"/> reflects all three assertions.
///
/// USAGE IN PLAY-MODE TESTS
/// ─────────────────────────
/// See PerformanceGate30NpcAt60FpsTests.cs — that test starts the scene, waits for
/// IsFinished, then reads LastResult and calls Assert directly.
/// </summary>
public sealed class PerformanceGate : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private FrameRateMonitor _frameRateMonitor;

    [Header("Gate thresholds (do not weaken — AT-11)")]
    [SerializeField] private float _minFpsThreshold  = 55f;
    [SerializeField] private float _meanFpsThreshold = 58f;
    [SerializeField] private float _p99FpsThreshold  = 50f;
    [SerializeField] private float _testDurationSecs = 60f;

    [Header("Behavior")]
    [Tooltip("Seconds to wait after scene load before measurement starts (JIT warm-up).")]
    [SerializeField] private float _warmUpSecs = 3f;

    // ── Results ───────────────────────────────────────────────────────────────

    /// <summary>True once the full measurement run is complete.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>True if all three threshold assertions passed.</summary>
    public bool Passed { get; private set; }

    /// <summary>Exact min/mean/p99 from the most recent run. Null until IsFinished.</summary>
    public GateResult LastResult { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator Start()
    {
        if (_frameRateMonitor == null)
            _frameRateMonitor = Object.FindObjectOfType<FrameRateMonitor>();

        if (_frameRateMonitor == null)
        {
            Debug.LogError("[PerformanceGate] FrameRateMonitor is not assigned. Attach one in the Inspector.");
            yield break;
        }

        // Warm-up: let JIT settle and the engine boot fully.
        yield return new WaitForSecondsRealtime(_warmUpSecs);

        _frameRateMonitor.ResetSamples();
        Debug.Log($"[PerformanceGate] Measurement started. Running for {_testDurationSecs}s. " +
                  $"Thresholds: min>={_minFpsThreshold} mean>={_meanFpsThreshold} p99>={_p99FpsThreshold}");

        yield return new WaitForSecondsRealtime(_testDurationSecs);

        // Collect results.
        float min  = _frameRateMonitor.SampleMin();
        float mean = _frameRateMonitor.SampleMean();
        float p99  = _frameRateMonitor.SampleP99();
        int   n    = _frameRateMonitor.SecondSamples.Count;

        LastResult = new GateResult(
            min, mean, p99,
            _minFpsThreshold, _meanFpsThreshold, _p99FpsThreshold,
            n);

        Passed = LastResult.AllPassed;

        string outcome = Passed ? "PASS" : "FAIL";
        string message = $"[PerformanceGate] {outcome} — {LastResult}";

        if (Passed)
            Debug.Log(message);
        else
            Debug.LogError(message);

        IsFinished = true;
    }
}

// ── Result value type ─────────────────────────────────────────────────────────

/// <summary>
/// Immutable measurement snapshot from one PerformanceGate run.
/// </summary>
public sealed class GateResult
{
    public float Min           { get; }
    public float Mean          { get; }
    public float P99           { get; }
    public float MinThreshold  { get; }
    public float MeanThreshold { get; }
    public float P99Threshold  { get; }
    public int   SampleCount   { get; }

    public bool MinPassed  => Min  >= MinThreshold;
    public bool MeanPassed => Mean >= MeanThreshold;
    public bool P99Passed  => P99  >= P99Threshold;
    public bool AllPassed  => MinPassed && MeanPassed && P99Passed;

    public GateResult(float min, float mean, float p99,
                      float minThreshold, float meanThreshold, float p99Threshold,
                      int sampleCount)
    {
        Min           = min;
        Mean          = mean;
        P99           = p99;
        MinThreshold  = minThreshold;
        MeanThreshold = meanThreshold;
        P99Threshold  = p99Threshold;
        SampleCount   = sampleCount;
    }

    public override string ToString() =>
        $"min:{Min:F1}/{MinThreshold} mean:{Mean:F1}/{MeanThreshold} p99:{P99:F1}/{P99Threshold} " +
        $"({SampleCount} samples) → {(AllPassed ? "PASS" : "FAIL")}";
}
