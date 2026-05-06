using System;
using APIFramework.Cast;
using APIFramework.Cast.Internal;
using Xunit;

namespace APIFramework.Tests.Cast;

/// <summary>
/// Per-tier title chance + structure. Statistical assertions over N samples.
/// </summary>
public class TitleBuilderTests
{
    private static readonly CastNameData Data = CastNameDataLoader.LoadDefault()!;

    [Fact]
    public void Common_AlwaysReturnsNull()
    {
        var rng = new Random(1);
        for (int i = 0; i < 200; i++)
            Assert.Null(TitleBuilder.Build(CastNameTier.Common, Data, rng));
    }

    [Fact]
    public void Uncommon_TitleChance_ApproximatelyFifteenPercent()
    {
        var rng     = new Random(101);
        var titled  = 0;
        const int N = 5000;
        for (int i = 0; i < N; i++)
            if (TitleBuilder.Build(CastNameTier.Uncommon, Data, rng) is not null) titled++;
        var rate = titled / (double)N;
        // Allow ±3 percentage-point band around 15%.
        Assert.InRange(rate, 0.12, 0.18);
    }

    [Fact]
    public void Rare_TitleChance_ApproximatelyFortyPercent()
    {
        var rng     = new Random(202);
        var titled  = 0;
        const int N = 5000;
        for (int i = 0; i < N; i++)
            if (TitleBuilder.Build(CastNameTier.Rare, Data, rng) is not null) titled++;
        var rate = titled / (double)N;
        Assert.InRange(rate, 0.36, 0.44);
    }

    [Fact]
    public void Epic_TitleChance_ApproximatelyEightyPercent()
    {
        var rng     = new Random(303);
        var titled  = 0;
        const int N = 5000;
        for (int i = 0; i < N; i++)
            if (TitleBuilder.Build(CastNameTier.Epic, Data, rng) is not null) titled++;
        var rate = titled / (double)N;
        Assert.InRange(rate, 0.76, 0.84);
    }

    [Fact]
    public void Legendary_AlwaysProducesThreeWordTitle()
    {
        var rng = new Random(404);
        for (int i = 0; i < 200; i++)
        {
            var t = TitleBuilder.Build(CastNameTier.Legendary, Data, rng);
            Assert.NotNull(t);
            Assert.True(t!.Split(' ').Length >= 3, $"Expected ≥3 words, got: '{t}'");
        }
    }

    [Fact]
    public void Mythic_AlwaysContainsOf()
    {
        var rng = new Random(505);
        for (int i = 0; i < 200; i++)
        {
            var t = TitleBuilder.Build(CastNameTier.Mythic, Data, rng);
            Assert.NotNull(t);
            Assert.Contains(" of ", t);
        }
    }
}
