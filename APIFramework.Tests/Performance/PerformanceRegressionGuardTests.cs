using System.Diagnostics;
using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Performance;

/// <summary>
/// Hard performance regression guards. Generous thresholds — catches engine-level
/// regressions (accidental O(N²), runaway allocations, infinite loops) without
/// flagging on noise.
///
/// PURPOSE
/// ───────
/// `SystemBenchmarkBaselineTests` regenerates the perf fixture every run; a
/// human review of the fixture diff catches subtle regressions during PR
/// review. But fixture diff is process; THIS test is automation — it fails
/// the build if perf regresses by >10× the current measured baseline. That's
/// the "we accidentally introduced a 60-second tick" guard.
///
/// CALIBRATION
/// ───────────
/// Thresholds are deliberately ~10× current baseline so that:
///   - Real perf regressions (someone added O(N²) loop) trip the guard
///   - Normal noise (CI thermal throttling, concurrent test load) doesn't
///   - Fixture-diff reviews still catch subtle 25–50% regressions during PR
///
/// Current baselines (clean isolated runs as of 2026-05-04):
///   - 30-NPC tick:   ~0.35 ms median
///   - 100-NPC tick:  ~1.20 ms median
///
/// Ceilings (this packet):
///   - 30-NPC tick:   < 5.0 ms median (~14× headroom)
///   - 100-NPC tick:  < 16.0 ms median (full 60-FPS frame budget; if we
///                     can't tick the engine in one frame, we have a problem)
///
/// IF THESE FIRE, INVESTIGATE
/// ───────────────────────────
/// A failure here is a flag for review, not a "definitely broken" signal —
/// CI machines vary. If it fires consistently across multiple runs / commits,
/// something genuinely regressed. Bisect with `git bisect` against the
/// fixture diffs to localize.
/// </summary>
public class PerformanceRegressionGuardTests
{
    private const float TickDelta       = 1f;
    private const int   WarmupTicks     = 30;
    private const int   MeasuredTicks   = 100;

    /// <summary>30-NPC tick must complete in well under one 60-FPS frame budget.</summary>
    [Fact]
    public void Guard_30Npcs_TickMedianUnder5Ms()
    {
        var medianMs = MedianTickMs(humanCount: 30);
        Assert.True(medianMs < 5.0,
            $"30-NPC tick median exceeded 5 ms ({medianMs:F3} ms). Engine perf regressed; " +
            $"investigate via SystemBenchmarkBaselineTests fixture diff.");
    }

    /// <summary>100-NPC tick must complete within one 60-FPS frame budget (16 ms).</summary>
    [Fact]
    public void Guard_100Npcs_TickMedianUnderFrameBudget()
    {
        var medianMs = MedianTickMs(humanCount: 100);
        Assert.True(medianMs < 16.0,
            $"100-NPC tick median exceeded 16 ms ({medianMs:F3} ms). Engine cannot sustain " +
            $"60 FPS with 100 NPCs — perf regression. Investigate via SystemBenchmarkBaselineTests.");
    }

    /// <summary>
    /// Scaling check: 100-NPC tick should not exceed 30-NPC tick by more than ~10×.
    /// Catches accidental super-linear (O(N²)) growth in any system.
    /// </summary>
    [Fact]
    public void Guard_NpcScaling_HundredNotMoreThanTenTimesThirty()
    {
        var thirty  = MedianTickMs(humanCount: 30);
        var hundred = MedianTickMs(humanCount: 100);
        Assert.True(hundred < thirty * 10.0,
            $"NPC scaling ratio is broken: 100-NPC tick ({hundred:F3} ms) is more than " +
            $"10× the 30-NPC tick ({thirty:F3} ms). Indicates O(N²) or worse — investigate.");
    }

    private static double MedianTickMs(int humanCount)
    {
        var sim = new SimulationBootstrapper(humanCount: humanCount);
        for (int i = 0; i < WarmupTicks; i++) sim.Engine.Update(TickDelta);

        var samples = new double[MeasuredTicks];
        var sw = new Stopwatch();
        for (int i = 0; i < MeasuredTicks; i++)
        {
            sw.Restart();
            sim.Engine.Update(TickDelta);
            sw.Stop();
            samples[i] = sw.Elapsed.TotalMilliseconds;
        }
        System.Array.Sort(samples);
        return samples[samples.Length / 2];
    }
}
