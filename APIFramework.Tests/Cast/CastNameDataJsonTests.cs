using System.Text.Json;
using APIFramework.Cast;
using Warden.Contracts;
using Xunit;

namespace APIFramework.Tests.Cast;

/// <summary>
/// JSON round-trip: serialise the loaded catalog, deserialise back, verify equivalence on key fields.
/// Catches regressions in JsonOptions.Wire compatibility for the CastNameData POCO shape.
/// </summary>
public class CastNameDataJsonTests
{
    [Fact]
    public void RoundTrip_PreservesAllRequiredBlocks()
    {
        var original = CastNameDataLoader.LoadDefault()!;

        var json = JsonSerializer.Serialize(original, JsonOptions.Wire);
        Assert.False(string.IsNullOrWhiteSpace(json));

        var roundTrip = JsonSerializer.Deserialize<CastNameData>(json, JsonOptions.Wire);
        Assert.NotNull(roundTrip);

        Assert.Equal(original.SchemaVersion, roundTrip!.SchemaVersion);
        Assert.Equal(original.FirstNames["male"].Length,    roundTrip.FirstNames["male"].Length);
        Assert.Equal(original.FirstNames["female"].Length,  roundTrip.FirstNames["female"].Length);
        Assert.Equal(original.FirstNames["neutral"].Length, roundTrip.FirstNames["neutral"].Length);
        Assert.Equal(original.FusionPrefixes.Length,        roundTrip.FusionPrefixes.Length);
        Assert.Equal(original.FusionSuffixes.Length,        roundTrip.FusionSuffixes.Length);
        Assert.Equal(original.StaticLastNames.Length,       roundTrip.StaticLastNames.Length);
        Assert.Equal(original.LegendaryRoots["male"].Length,    roundTrip.LegendaryRoots["male"].Length);
        Assert.Equal(original.LegendaryTitles.Length,       roundTrip.LegendaryTitles.Length);
        Assert.Equal(original.CorporateTitles.Length,       roundTrip.CorporateTitles.Length);
        Assert.Equal(original.TitleTiers.Rank.Mundane.Length, roundTrip.TitleTiers.Rank.Mundane.Length);
    }

    [Fact]
    public void TierThresholds_AreMonotonicallyIncreasing()
    {
        var data = CastNameDataLoader.LoadDefault()!;
        var t = data.TierThresholds;
        Assert.True(t.Common    < t.Uncommon);
        Assert.True(t.Uncommon  < t.Rare);
        Assert.True(t.Rare      < t.Epic);
        Assert.True(t.Epic      < t.Legendary);
        Assert.True(t.Legendary < t.Mythic);
        Assert.True(t.Mythic   <= 1.0);
    }

    [Fact]
    public void DefaultCatalog_IncludesBadgeFlairAndDepartmentStamps()
    {
        // Optional but expected on the canonical catalog (Talon's port preserves them
        // for the future badge-generation packet).
        var data = CastNameDataLoader.LoadDefault()!;
        Assert.NotNull(data.BadgeFlair);
        Assert.NotNull(data.DepartmentStamps);
        Assert.NotEmpty(data.DepartmentStamps!);
    }
}
