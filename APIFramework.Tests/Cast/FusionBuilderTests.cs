using System;
using APIFramework.Cast;
using APIFramework.Cast.Internal;
using Xunit;

namespace APIFramework.Tests.Cast;

/// <summary>
/// AT-11 — cleanFusion regex behavior; surname builder structure.
/// </summary>
public class FusionBuilderTests
{
    [Theory]
    [InlineData("Sackker",   "Sacker")]      // "ckk" awkward-cluster fix
    [InlineData("dressss",   "dress")]       // 4 s's collapse to 2
    [InlineData("kkkk",      "kk")]          // 4 k's → triple-collapse to 2; the kkk→k overlap rule
                                             // doesn't re-fire (JS source same behavior).
    [InlineData("hellottbox","hellotbox")]   // ttb cluster fix
    [InlineData("plain",     "plain")]       // no change
    public void CleanFusion_CollapsesPerJsRules(string input, string expected)
    {
        Assert.Equal(expected, FusionBuilder.CleanFusion(input));
    }

    [Fact]
    public void CleanFusion_TripleConsonantCollapse_CaseInsensitive()
    {
        Assert.Equal("Tomm",  FusionBuilder.CleanFusion("Tommm"));
        Assert.Equal("RuMM",  FusionBuilder.CleanFusion("RuMMM"));
    }

    [Fact]
    public void BuildSurname_ReturnsNonEmpty()
    {
        var data = CastNameDataLoader.LoadDefault()!;
        var rng  = new Random(42);
        for (int i = 0; i < 50; i++)
        {
            var s = FusionBuilder.BuildSurname(data, rng);
            Assert.False(string.IsNullOrWhiteSpace(s));
        }
    }

    [Fact]
    public void BuildShortSurnameHalf_PrefersShortInputs()
    {
        var data = CastNameDataLoader.LoadDefault()!;
        var rng  = new Random(7);
        // Short halves are bounded: short static (≤8) OR short prefix (≤5) + short suffix (≤4) = ≤9.
        // Allow some slack for the regex but the upper bound is well below 15.
        for (int i = 0; i < 100; i++)
        {
            var s = FusionBuilder.BuildShortSurnameHalf(data, rng);
            Assert.False(string.IsNullOrWhiteSpace(s));
            Assert.True(s.Length <= 12, $"BuildShortSurnameHalf produced too-long output: '{s}' ({s.Length} chars)");
        }
    }
}
