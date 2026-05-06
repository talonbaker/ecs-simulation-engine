using System;
using System.Collections.Generic;
using APIFramework.Cast;
using APIFramework.Hire;
using Xunit;

namespace APIFramework.Tests.Hire;

public class HirePerkTests
{
    private static readonly CastNameData     Data    = CastNameDataLoader.LoadDefault()!;
    private static readonly CastNameGenerator NameGen = new(Data);

    // ── LuckyHirePerk shifts thresholds correctly ───────────────────────────────

    [Fact]
    public void LuckyHirePerk_Apply_ShiftsThresholdsDown_PreservesMythicCeiling()
    {
        var baseT = new TierThresholdsDto();   // 0.55 / 0.82 / 0.94 / 0.98 / 0.995 / 1.0
        var perk  = new LuckyHirePerk(0.10);

        var shifted = perk.Apply(baseT);

        Assert.Equal(0.45,  shifted.Common,    3);
        Assert.Equal(0.72,  shifted.Uncommon,  3);
        Assert.Equal(0.84,  shifted.Rare,      3);
        Assert.Equal(0.88,  shifted.Epic,      3);
        Assert.Equal(0.895, shifted.Legendary, 3);
        Assert.Equal(1.0,   shifted.Mythic,    3);   // mythic ceiling preserved
    }

    [Fact]
    public void LuckyHirePerk_LargeShift_ClampsAtZero()
    {
        var baseT = new TierThresholdsDto();
        var perk  = new LuckyHirePerk(2.0);   // would push thresholds negative

        var shifted = perk.Apply(baseT);

        Assert.True(shifted.Common    >= 0.0);
        Assert.True(shifted.Uncommon  >= 0.0);
        Assert.True(shifted.Rare      >= 0.0);
        Assert.True(shifted.Epic      >= 0.0);
        Assert.True(shifted.Legendary >= 0.0);
        Assert.Equal(1.0, shifted.Mythic);
    }

    [Fact]
    public void LuckyHirePerk_Apply_DoesNotMutateInputThresholds()
    {
        var baseT = new TierThresholdsDto();
        var originalCommon = baseT.Common;
        new LuckyHirePerk(0.10).Apply(baseT);
        Assert.Equal(originalCommon, baseT.Common);
    }

    // ── Empirical distribution: LuckyHirePerk should bias toward higher tiers ───

    [Fact]
    public void HireSession_WithLuckyPerk_ShiftsDistributionTowardRarePlus()
    {
        const int N = 5000;
        var wallet = new HireRerollWallet(0);
        var config = new HireRerollConfig();

        // Baseline: no perks. Count how many initial-rolls land in Rare+ tier.
        var serviceBase = new HireRerollService(NameGen, wallet, config);
        var baseRarePlus = 0;
        for (int i = 0; i < N; i++)
        {
            var s = serviceBase.Begin(rng: new Random(i));
            if ((int)s.CurrentCandidate.Tier >= (int)CastNameTier.Rare) baseRarePlus++;
        }

        // With perk: 0.10 shift down. Expect Rare+ rate noticeably higher.
        var serviceLucky = new HireRerollService(NameGen, wallet, config);
        var luckyPerks   = new List<HirePerk> { new LuckyHirePerk(0.10) };
        var luckyRarePlus = 0;
        for (int i = 0; i < N; i++)
        {
            var s = serviceLucky.Begin(rng: new Random(N + i), perks: luckyPerks);
            if ((int)s.CurrentCandidate.Tier >= (int)CastNameTier.Rare) luckyRarePlus++;
        }

        var baseRate  = baseRarePlus  / (double)N;
        var luckyRate = luckyRarePlus / (double)N;

        // Baseline Rare+ rate is 0.18 (12% Rare + 4% Epic + 1.5% Leg + 0.5% Myth).
        // Perk shifts each threshold down 0.10 — Rare+ rate becomes ~0.28.
        // Allow generous margin for sampling noise but require a real shift.
        Assert.True(luckyRate > baseRate + 0.05,
            $"Expected luckyRate ({luckyRate:F3}) to exceed baseRate ({baseRate:F3}) by ≥ 5pp");
    }
}
