using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rolling-average FPS sampler.
///
/// Samples the instantaneous frame rate every frame and maintains:
///   • A rolling 60-frame average (smooth display value).
///   • A 1-second sampled list (for the performance gate's 1 Hz measurement).
///
/// THREAD SAFETY
/// ─────────────
/// Not thread-safe. Only call from Unity's main thread (Update / LateUpdate).
///
/// USAGE
/// ─────
/// Attach to any persistent GameObject. Other components read:
///   FrameRateMonitor.RollingAvgFps   — current rolling average
///   FrameRateMonitor.SecondSamples   — list of 1-Hz FPS samples (for gate tests)
///   FrameRateMonitor.LastFps         — instantaneous FPS this frame
/// </summary>
public sealed class FrameRateMonitor : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("Number of frames in the rolling average window.")]
    private int _rollingWindowFrames = 60;

    // ── Rolling average state ─────────────────────────────────────────────────

    private readonly Queue<float> _frameTimeQueue = new();
    private float _frameTimeSum = 0f;

    // ── 1-Hz sample state ─────────────────────────────────────────────────────

    // Accumulated real seconds in the current 1-second bucket.
    private float _secondAccumulator   = 0f;
    // Accumulated frame count in the current 1-second bucket.
    private int   _framesInSecond      = 0;

    // ── Public accessors ──────────────────────────────────────────────────────

    /// <summary>Instantaneous FPS this frame (1 / Time.deltaTime).</summary>
    public float LastFps { get; private set; }

    /// <summary>Rolling average FPS over the last <see cref="_rollingWindowFrames"/> frames.</summary>
    public float RollingAvgFps { get; private set; }

    /// <summary>
    /// List of per-second average FPS samples, appended at 1 Hz.
    /// <see cref="PerformanceGate"/> reads this list for the 60-second test.
    /// </summary>
    public List<float> SecondSamples { get; } = new();

    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;   // unscaled: unaffected by Time.timeScale

        // Avoid division by zero on the first frame (or if DT is somehow 0).
        if (dt <= 0f) return;

        LastFps = 1f / dt;

        // ── Rolling average ────────────────────────────────────────────────

        _frameTimeQueue.Enqueue(dt);
        _frameTimeSum += dt;

        while (_frameTimeQueue.Count > _rollingWindowFrames)
            _frameTimeSum -= _frameTimeQueue.Dequeue();

        RollingAvgFps = _frameTimeQueue.Count > 0
            ? _frameTimeQueue.Count / _frameTimeSum
            : 0f;

        // ── 1-Hz sampling ──────────────────────────────────────────────────

        _secondAccumulator += dt;
        _framesInSecond++;

        if (_secondAccumulator >= 1f)
        {
            float sampleFps = _framesInSecond / _secondAccumulator;
            SecondSamples.Add(sampleFps);

            _secondAccumulator = 0f;
            _framesInSecond    = 0;
        }
    }

    // ── Statistical helpers (for PerformanceGate) ─────────────────────────────

    /// <summary>
    /// Returns the minimum value in <see cref="SecondSamples"/>, or 0 if no samples.
    /// </summary>
    public float SampleMin()
    {
        if (SecondSamples.Count == 0) return 0f;
        float min = float.MaxValue;
        foreach (var s in SecondSamples) if (s < min) min = s;
        return min;
    }

    /// <summary>
    /// Returns the mean of <see cref="SecondSamples"/>, or 0 if no samples.
    /// </summary>
    public float SampleMean()
    {
        if (SecondSamples.Count == 0) return 0f;
        float sum = 0f;
        foreach (var s in SecondSamples) sum += s;
        return sum / SecondSamples.Count;
    }

    /// <summary>
    /// Returns the p99 (99th percentile) of <see cref="SecondSamples"/>, or 0 if fewer than 2 samples.
    /// p99 = the value at or below which 99% of samples fall.
    /// </summary>
    public float SampleP99()
    {
        int count = SecondSamples.Count;
        if (count < 2) return SampleMin();

        var sorted = new List<float>(SecondSamples);
        sorted.Sort();

        // Index for p99: 1% from the bottom of a sorted ascending list (p99 is a floor value).
        // We want the 1st-percentile value (floor), which is the worst 1%.
        int idx = Mathf.Max(0, Mathf.FloorToInt(count * 0.01f));
        return sorted[idx];
    }

    /// <summary>Clears all recorded samples. Used by PerformanceGate before starting a test run.</summary>
    public void ResetSamples()
    {
        SecondSamples.Clear();
        _secondAccumulator = 0f;
        _framesInSecond    = 0;
    }
}
