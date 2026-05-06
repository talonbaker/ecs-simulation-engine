using System;
using APIFramework.Cast;
using Xunit;

namespace APIFramework.Tests.Cast;

/// <summary>
/// AT-01..AT-09 — per-tier output structure + determinism.
/// </summary>
public class CastNameGeneratorTests
{
    private static readonly CastNameData Data = CastNameDataLoader.LoadDefault()!;
    private static readonly CastNameGenerator Gen = new(Data);

    // ── Determinism ───────────────────────────────────────────────────────────────

    [Fact]
    public void AT01_SameSeed_ProducesIdenticalResult()
    {
        var a = Gen.Generate(seed: 42);
        var b = Gen.Generate(seed: 42);
        Assert.Equal(a, b);  // record equality
    }

    [Fact]
    public void AT02_DifferentSeeds_ProduceDifferentDisplayName()
    {
        // Pick a handful of seed pairs; the cardinality of the output space is huge,
        // so even one pair almost always differs — but we run several to make the assert robust.
        var differs = 0;
        for (int i = 0; i < 20; i++)
        {
            var a = Gen.Generate(seed: i);
            var b = Gen.Generate(seed: i + 9999);
            if (a.DisplayName != b.DisplayName) differs++;
        }
        Assert.True(differs >= 19, $"Expected nearly all 20 paired seeds to differ; only {differs}/20 did.");
    }

    // ── Per-tier structure ────────────────────────────────────────────────────────

    [Fact]
    public void AT04_Common_ShapeIsFirstSpaceStaticSurname()
    {
        var rng = new Random(1000);
        for (int i = 0; i < 100; i++)
        {
            var r = Gen.Generate(rng, forcedTier: CastNameTier.Common);
            Assert.Equal(CastNameTier.Common, r.Tier);
            Assert.Equal($"{r.FirstName} {r.Surname}", r.DisplayName);
            Assert.NotNull(r.Surname);
            Assert.Null(r.Title);
            Assert.Null(r.LegendaryRoot);
            Assert.Null(r.LegendaryTitle);
            Assert.Null(r.CorporateTitle);
            Assert.Contains(r.Surname!, Data.StaticLastNames);
        }
    }

    [Fact]
    public void AT05_Uncommon_HasFusedSurname()
    {
        var rng = new Random(2000);
        for (int i = 0; i < 100; i++)
        {
            var r = Gen.Generate(rng, forcedTier: CastNameTier.Uncommon);
            Assert.Equal(CastNameTier.Uncommon, r.Tier);
            Assert.NotNull(r.Surname);
            Assert.Equal($"{r.FirstName} {r.Surname}", r.DisplayName);
            Assert.Null(r.LegendaryRoot);
            Assert.Null(r.CorporateTitle);
        }
    }

    [Fact]
    public void AT06_Rare_HyphenRateApproximatelyFifteenPercent()
    {
        var rng     = new Random(3000);
        const int N = 4000;
        var hyphenated = 0;
        var titled     = 0;
        for (int i = 0; i < N; i++)
        {
            var r = Gen.Generate(rng, forcedTier: CastNameTier.Rare);
            Assert.Equal(CastNameTier.Rare, r.Tier);
            Assert.NotNull(r.Surname);
            if (r.Surname!.Contains('-')) hyphenated++;
            if (r.Title is not null) titled++;
        }
        Assert.InRange(hyphenated / (double)N, 0.12, 0.18);
        Assert.InRange(titled     / (double)N, 0.36, 0.44);
    }

    [Fact]
    public void AT07_Epic_AlwaysHasCorporateTitle_HyphenRateApproximatelyThirty()
    {
        var rng     = new Random(4000);
        const int N = 4000;
        var hyphenated = 0;
        for (int i = 0; i < N; i++)
        {
            var r = Gen.Generate(rng, forcedTier: CastNameTier.Epic);
            Assert.Equal(CastNameTier.Epic, r.Tier);
            Assert.NotNull(r.CorporateTitle);
            Assert.Contains(r.CorporateTitle!, Data.CorporateTitles);
            Assert.NotNull(r.Surname);
            Assert.Equal($"{r.CorporateTitle} {r.FirstName} {r.Surname}", r.DisplayName);
            if (r.Surname!.Contains('-')) hyphenated++;
        }
        Assert.InRange(hyphenated / (double)N, 0.26, 0.34);
    }

    [Fact]
    public void AT08_Legendary_DivineAndHybridSplit_ApproximatelyFiftyFifty()
    {
        var rng     = new Random(5000);
        const int N = 4000;
        var divine = 0;
        var hybrid = 0;
        for (int i = 0; i < N; i++)
        {
            var r = Gen.Generate(rng, forcedTier: CastNameTier.Legendary);
            Assert.Equal(CastNameTier.Legendary, r.Tier);
            Assert.NotNull(r.LegendaryRoot);
            if (r.CorporateTitle is null) divine++; else hybrid++;
        }
        Assert.InRange(divine / (double)N, 0.46, 0.54);
        Assert.InRange(hybrid / (double)N, 0.46, 0.54);
    }

    [Fact]
    public void AT09_Mythic_HasCorpRootAndTitle()
    {
        var rng = new Random(6000);
        for (int i = 0; i < 200; i++)
        {
            var r = Gen.Generate(rng, forcedTier: CastNameTier.Mythic);
            Assert.Equal(CastNameTier.Mythic, r.Tier);
            Assert.NotNull(r.CorporateTitle);
            Assert.NotNull(r.LegendaryRoot);
            Assert.NotNull(r.LegendaryTitle);
            Assert.Equal($"{r.CorporateTitle} {r.LegendaryRoot}, {r.LegendaryTitle}", r.DisplayName);
        }
    }

    [Fact]
    public void AllTiers_FirstNameDrawnFromCorrectGenderPool()
    {
        var rng = new Random(7000);
        foreach (var g in new[] { CastGender.Male, CastGender.Female, CastGender.Neutral })
        {
            var pool = g switch
            {
                CastGender.Male   => Data.FirstNames["male"],
                CastGender.Female => Data.FirstNames["female"],
                _                 => Data.FirstNames["neutral"],
            };
            for (int i = 0; i < 30; i++)
            {
                var r = Gen.Generate(rng, gender: g);
                Assert.Contains(r.FirstName, pool);
                Assert.Equal(g, r.Gender);
            }
        }
    }

    [Fact]
    public void NullRng_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Gen.Generate(rng: null!));
    }
}
