using System;
using System.Collections.Generic;
using APIFramework.Cast;
using Xunit;

namespace APIFramework.Tests.Cast;

/// <summary>
/// AT-10 — empirical distribution over 100k rolls within ±1.5% of declared thresholds for each tier.
/// </summary>
public class TierDistributionTests
{
    private static readonly CastNameData Data = CastNameDataLoader.LoadDefault()!;
    private static readonly CastNameGenerator Gen = new(Data);

    [Fact]
    public void TierDistribution_OverHundredKRolls_WithinOnePointFivePercent()
    {
        const int N = 100_000;
        var rng    = new Random(13);
        var counts = new Dictionary<CastNameTier, int>();
        foreach (CastNameTier t in Enum.GetValues(typeof(CastNameTier))) counts[t] = 0;

        for (int i = 0; i < N; i++)
        {
            var r = Gen.Generate(rng);
            counts[r.Tier]++;
        }

        // Expected (cumulative-difference of thresholds): 0.55 / 0.27 / 0.12 / 0.04 / 0.015 / 0.005
        var expected = new Dictionary<CastNameTier, double>
        {
            { CastNameTier.Common,    0.55  },
            { CastNameTier.Uncommon,  0.27  },
            { CastNameTier.Rare,      0.12  },
            { CastNameTier.Epic,      0.04  },
            { CastNameTier.Legendary, 0.015 },
            { CastNameTier.Mythic,    0.005 },
        };

        foreach (var (tier, expRate) in expected)
        {
            var actualRate = counts[tier] / (double)N;
            // ±1.5% absolute band: large enough N and large enough tolerance for the rarer tiers.
            // For rarest (mythic 0.5%), absolute band of 0.015 covers ±300% relative — that's intentional;
            // the relative noise on a ~500/100k count is naturally large.
            Assert.InRange(actualRate, expRate - 0.015, expRate + 0.015);
        }
    }
}
