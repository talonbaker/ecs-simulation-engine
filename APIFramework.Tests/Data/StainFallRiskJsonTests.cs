using System;
using System.Linq;
using Xunit;
using APIFramework.Bootstrap;

namespace APIFramework.Tests.Data;

public class StainFallRiskJsonTests
{
    [Fact]
    public void StainFallRiskJson_LoadsSuccessfully()
    {
        var catalog = StainFallRiskLoader.LoadDefault();
        Assert.NotNull(catalog);
    }

    [Fact]
    public void StainFallRiskJson_HasAllRequiredStainKinds()
    {
        var catalog = StainFallRiskLoader.LoadDefault();
        Assert.NotNull(catalog);
        Assert.NotNull(catalog.StainKindFallRisk);

        var kinds = catalog.StainKindFallRisk.Select(x => x.Kind).ToHashSet();
        Assert.Contains("water", kinds);
        Assert.Contains("blood", kinds);
        Assert.Contains("oil", kinds);
        Assert.Contains("coffee", kinds);
        Assert.Contains("vomit", kinds);
        Assert.Contains("urine", kinds);
        Assert.Contains("broken-glass", kinds);
    }

    [Fact]
    public void StainFallRiskJson_AllValuesInValidRange()
    {
        var catalog = StainFallRiskLoader.LoadDefault();
        Assert.NotNull(catalog);
        Assert.NotNull(catalog.StainKindFallRisk);

        foreach (var entry in catalog.StainKindFallRisk)
        {
            Assert.True(entry.FallRiskLevel >= 0.0f && entry.FallRiskLevel <= 1.0f,
                $"Entry {entry.Kind} has out-of-range risk level: {entry.FallRiskLevel}");
        }
    }

    [Theory]
    [InlineData("water", 0.40f)]
    [InlineData("blood", 0.60f)]
    [InlineData("oil", 0.85f)]
    public void GetFallRiskForKind_ReturnsCorrectValue(string kind, float expectedRisk)
    {
        float actualRisk = StainFallRiskLoader.GetFallRiskForKind(kind);
        Assert.Equal(expectedRisk, actualRisk);
    }

    [Fact]
    public void GetFallRiskForKind_ReturnsZeroForUnknownKind()
    {
        float risk = StainFallRiskLoader.GetFallRiskForKind("unknown-stain-type");
        Assert.Equal(0.0f, risk);
    }
}
