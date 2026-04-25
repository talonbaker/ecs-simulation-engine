using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// Tests for DriveDynamicsSystem: decay, circadian shape, and volatility scaling.
/// All tests use deterministic seeds; no wall-clock time.
/// </summary>
public class DriveDynamicsSystemTests
{
    private static SocialSystemConfig DefaultCfg() => new();
    private static SimulationClock    MakeClock(double dayFraction = 0.0)
    {
        // Advance the clock to the desired day fraction.
        var clock = new SimulationClock();
        // GameTimeOfDay = TotalTime + StartOffset (6h). To reach target fraction:
        // fraction = (TotalTime + 21600) / 86400  →  TotalTime = fraction*86400 - 21600
        // For fraction 0: TotalTime = -21600 (clock resets; just use 0 and dayFraction≈0.25)
        // Simpler: advance clock by ticking it. Clock starts at 6 AM = 0.25 of day.
        // We'll expose the clock and let tests pass in desired total time via Tick.
        return clock;
    }

    private static (EntityManager em, Entity entity) BuildNpc(
        int currentLoneliness = 50, int baselineLoneliness = 50, int neuroticism = 0)
    {
        var em = new EntityManager();
        var e  = em.CreateEntity();
        e.Add(new NpcTag());
        e.Add(new SocialDrivesComponent
        {
            Loneliness = new DriveValue { Current = currentLoneliness, Baseline = baselineLoneliness }
        });
        e.Add(new PersonalityComponent(0, 0, 0, 0, neuroticism));
        return (em, e);
    }

    // AT-02: 1000 ticks, drive starts at 90 with baseline 50; should end within 5 points of 50
    [Fact]
    public void Decay_1000Ticks_DriveMoveTowardBaseline()
    {
        var cfg   = DefaultCfg();
        cfg.DriveCircadianAmplitudes["loneliness"] = 0.0;  // disable circadian for clean test
        cfg.DriveVolatilityScale = 0.0;                     // disable noise
        var clock = new SimulationClock();
        var rng   = new SeededRandom(42);
        var sys   = new DriveDynamicsSystem(cfg, clock, rng);

        var (em, entity) = BuildNpc(currentLoneliness: 90, baselineLoneliness: 50);

        for (int i = 0; i < 1000; i++)
            sys.Update(em, 1f);

        var final = entity.Get<SocialDrivesComponent>().Loneliness.Current;
        Assert.True(Math.Abs(final - 50) <= 5,
            $"Drive should be within 5 of baseline=50 after 1000 ticks, got {final}");
    }

    // AT-03: circadian peak for Loneliness at configured phase 0.85 of day
    [Fact]
    public void Circadian_LonelinessPeaksAtConfiguredPhase()
    {
        // Average (Current - Baseline) across 1000 seeds at the phase and at phase+0.5
        // At the peak phase, average should be positive; at anti-phase, negative.
        const double targetPhase = 0.85;
        const double antiPhase   = (targetPhase + 0.5) % 1.0;
        const int seeds = 1000;

        double sumAtPeak     = 0;
        double sumAtAntiPeak = 0;

        for (int seed = 0; seed < seeds; seed++)
        {
            sumAtPeak     += MeasureCircadianDelta(seed, targetPhase);
            sumAtAntiPeak += MeasureCircadianDelta(seed, antiPhase);
        }

        double avgAtPeak     = sumAtPeak     / seeds;
        double avgAtAntiPeak = sumAtAntiPeak / seeds;

        Assert.True(avgAtPeak > 0,
            $"Average delta at peak phase should be positive, got {avgAtPeak:F3}");
        Assert.True(avgAtPeak > avgAtAntiPeak,
            $"Peak delta ({avgAtPeak:F3}) should exceed anti-peak ({avgAtAntiPeak:F3})");
    }

    private static double MeasureCircadianDelta(int seed, double dayFraction)
    {
        var cfg = DefaultCfg();
        cfg.DriveVolatilityScale = 0.0;  // no noise
        cfg.DriveDecayPerTick    = 0.0;  // no decay

        // Create a clock at the desired day fraction.
        // GameTimeOfDay = (TotalTime + 21600) % 86400
        // Desired fraction: (TotalTime + 21600) % 86400 = dayFraction * 86400
        // → TotalTime = dayFraction * 86400 - 21600 (may be negative; wrap)
        double desiredTod   = dayFraction * SimulationClock.SecondsPerDay;
        const double startOffset = 6.0 * 3600.0;
        double totalTime    = desiredTod - startOffset;
        if (totalTime < 0) totalTime += SimulationClock.SecondsPerDay;

        var clock = new SimulationClock();
        clock.Tick((float)(totalTime / clock.TimeScale));   // advance to target fraction

        var rng = new SeededRandom(seed);
        var sys = new DriveDynamicsSystem(cfg, clock, rng);

        var em = new EntityManager();
        var e  = em.CreateEntity();
        e.Add(new NpcTag());
        e.Add(new SocialDrivesComponent
        {
            Loneliness = new DriveValue { Current = 50, Baseline = 50 }
        });
        e.Add(new PersonalityComponent(0, 0, 0, 0, 0));

        sys.Update(em, 1f);

        int after = e.Get<SocialDrivesComponent>().Loneliness.Current;
        return after - 50.0;
    }

    // AT-04: volatility scales with Neuroticism
    [Fact]
    public void Volatility_ScalesWithNeuroticism()
    {
        // Compare standard deviation of Current deltas for high vs low Neuroticism
        const int seeds = 5000;

        double varHigh = MeasureVolatilityVariance(neuroticism:  2, seeds: seeds);
        double varLow  = MeasureVolatilityVariance(neuroticism: -2, seeds: seeds);

        Assert.True(varHigh > varLow,
            $"High neuroticism variance ({varHigh:F4}) should exceed low ({varLow:F4})");
    }

    private static double MeasureVolatilityVariance(int neuroticism, int seeds)
    {
        var cfg = DefaultCfg();
        cfg.DriveCircadianAmplitudes["loneliness"] = 0.0;  // isolate noise
        cfg.DriveDecayPerTick = 0.0;

        var deltas = new double[seeds];
        for (int s = 0; s < seeds; s++)
        {
            var clock = new SimulationClock();
            var rng   = new SeededRandom(s);
            var sys   = new DriveDynamicsSystem(cfg, clock, rng);

            var em = new EntityManager();
            var e  = em.CreateEntity();
            e.Add(new NpcTag());
            e.Add(new SocialDrivesComponent
            {
                Loneliness = new DriveValue { Current = 50, Baseline = 50 }
            });
            e.Add(new PersonalityComponent(0, 0, 0, 0, neuroticism));

            sys.Update(em, 1f);
            deltas[s] = e.Get<SocialDrivesComponent>().Loneliness.Current - 50.0;
        }

        // Variance
        double mean     = 0;
        foreach (var d in deltas) mean += d;
        mean /= seeds;

        double variance = 0;
        foreach (var d in deltas) variance += (d - mean) * (d - mean);
        return variance / seeds;
    }

    [Fact]
    public void EntityWithoutSocialDrives_Skipped()
    {
        var cfg = DefaultCfg();
        var em  = new EntityManager();
        var e   = em.CreateEntity();
        e.Add(new NpcTag());
        // No SocialDrivesComponent

        var sys = new DriveDynamicsSystem(cfg, new SimulationClock(), new SeededRandom(0));
        var ex  = Record.Exception(() => sys.Update(em, 1f));
        Assert.Null(ex);
    }

    [Fact]
    public void NonNpcEntity_NotProcessed()
    {
        var cfg = DefaultCfg();
        var em  = new EntityManager();
        var e   = em.CreateEntity();
        e.Add(new SocialDrivesComponent
        {
            Loneliness = new DriveValue { Current = 90, Baseline = 50 }
        });
        e.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        // No NpcTag

        cfg.DriveVolatilityScale = 0.0;
        cfg.DriveCircadianAmplitudes["loneliness"] = 0.0;
        var sys = new DriveDynamicsSystem(cfg, new SimulationClock(), new SeededRandom(0));
        sys.Update(em, 1f);

        // Drive should be unchanged because the system skips non-NPC entities
        Assert.Equal(90, e.Get<SocialDrivesComponent>().Loneliness.Current);
    }
}
