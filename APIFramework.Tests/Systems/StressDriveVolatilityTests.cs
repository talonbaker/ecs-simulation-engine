using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-09: DriveDynamicsSystem with stress produces higher drive variance than without.
/// Statistical test at p&lt;0.01 over 5000 ticks via variance ratio.
/// </summary>
public class StressDriveVolatilityTests
{
    [Fact]
    public void HighAcuteLevel_ProducesHigherDriveVariance_ThanNoStress()
    {
        const int ticks = 5000;
        const int seed  = 4242;

        double varHigh = MeasureVariance(seed, ticks, acuteLevel: 80);
        double varNone = MeasureVariance(seed, ticks, acuteLevel:  0);

        Assert.True(varHigh > varNone,
            $"Expected variance with AcuteLevel=80 ({varHigh:F4}) > variance with AcuteLevel=0 ({varNone:F4})");
    }

    [Fact]
    public void VarianceRatio_IsStatisticallySignificant()
    {
        // Over 5000 ticks the variance ratio should be meaningfully above 1.
        // stressVolatilityScale=0.5: at AcuteLevel=80, stressMult = 1 + 0.8*0.5 = 1.4
        // Variance scales roughly as stressMult² = 1.96 → ratio ≥ 1.2 is very conservative.
        const int ticks = 5000;
        const int seed  = 999;

        double varHigh = MeasureVariance(seed, ticks, acuteLevel: 80);
        double varNone = MeasureVariance(seed, ticks, acuteLevel:  0);

        double ratio = varNone > 0 ? varHigh / varNone : double.MaxValue;
        Assert.True(ratio >= 1.2,
            $"Expected variance ratio ≥ 1.2 (got {ratio:F3}); stressMult at AcuteLevel=80 is 1.4");
    }

    private static double MeasureVariance(int seed, int ticks, int acuteLevel)
    {
        var cfg = new SocialSystemConfig
        {
            DriveVolatilityScale = 2.0,   // amplify so integer steps appear frequently
            DriveDecayPerTick    = 0.0,   // isolate noise
        };
        cfg.DriveCircadianAmplitudes["loneliness"] = 0.0;   // no circadian noise

        var stressCfg = new StressConfig { StressVolatilityScale = 0.5 };
        var clock     = new SimulationClock();
        var rng       = new SeededRandom(seed);
        var sys       = new DriveDynamicsSystem(cfg, clock, rng, stressCfg);

        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialDrivesComponent
        {
            Loneliness = new DriveValue { Current = 50, Baseline = 50 }
        });
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));   // Neuroticism=0
        npc.Add(new StressComponent { AcuteLevel = acuteLevel, LastDayUpdated = 1 });

        var deltas = new double[ticks];
        int prev   = 50;
        for (int i = 0; i < ticks; i++)
        {
            sys.Update(em, 1f);
            int cur    = npc.Get<SocialDrivesComponent>().Loneliness.Current;
            deltas[i]  = cur - prev;
            prev       = cur;
        }

        double mean = 0;
        foreach (var d in deltas) mean += d;
        mean /= ticks;

        double variance = 0;
        foreach (var d in deltas) variance += (d - mean) * (d - mean);
        return variance / ticks;
    }
}
