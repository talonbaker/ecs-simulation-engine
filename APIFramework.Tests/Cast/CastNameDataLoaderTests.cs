using System;
using System.IO;
using APIFramework.Cast;
using Xunit;

namespace APIFramework.Tests.Cast;

/// <summary>
/// AT-12 / AT-13 — catalog loader fails closed on missing required blocks; loads default cleanly.
/// </summary>
public class CastNameDataLoaderTests
{
    [Fact]
    public void DefaultCatalog_LoadsCleanly()
    {
        var path = CastNameDataLoader.FindDefault();
        Assert.NotNull(path);

        var data = CastNameDataLoader.Load(path!);

        Assert.NotNull(data);
        Assert.Equal("0.1.0", data.SchemaVersion);
        Assert.True(data.FirstNames.ContainsKey("male"));
        Assert.True(data.FirstNames.ContainsKey("female"));
        Assert.True(data.FirstNames.ContainsKey("neutral"));
        Assert.NotEmpty(data.StaticLastNames);
        Assert.NotEmpty(data.FusionPrefixes);
        Assert.NotEmpty(data.FusionSuffixes);
        Assert.NotEmpty(data.LegendaryRoots);
        Assert.NotEmpty(data.LegendaryTitles);
        Assert.NotEmpty(data.CorporateTitles);
        Assert.NotEmpty(data.TitleTiers.Rank.Mundane);
        Assert.NotEmpty(data.TitleTiers.Rank.HighStatus);
    }

    [Fact]
    public void CachedDefault_ReturnsSameInstance()
    {
        var a = CastNameDataLoader.LoadDefault();
        var b = CastNameDataLoader.LoadDefault();
        Assert.Same(a, b);
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => CastNameDataLoader.Load("does/not/exist.json"));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void Load_MissingFirstNames_FailsClosed()
    {
        var json = """
        {
          "schemaVersion": "0.1.0",
          "firstNames": {},
          "fusionPrefixes": ["A"],
          "fusionSuffixes": ["b"],
          "staticLastNames": ["X"],
          "legendaryRoots": { "male": ["Z"], "female": ["A"], "neutral": ["N"] },
          "legendaryTitles": ["T"],
          "corporateTitles": ["C"],
          "titleTiers": {
            "rank":     { "mundane": ["r"], "silly": ["s"], "highStatus": ["H"] },
            "domain":   { "mundane": ["d"], "silly": ["s"], "highStatus": ["D"] },
            "function": { "mundane": ["f"], "silly": ["s"], "highStatus": ["F"] }
          }
        }
        """;
        var tmp = Path.Combine(Path.GetTempPath(), $"name-data-{Guid.NewGuid():N}.json");
        File.WriteAllText(tmp, json);
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => CastNameDataLoader.Load(tmp));
            Assert.Contains("firstNames", ex.Message);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void Load_MissingLegendaryNeutral_FailsClosed()
    {
        var json = """
        {
          "schemaVersion": "0.1.0",
          "firstNames": { "male": ["X"], "female": ["Y"], "neutral": ["Z"] },
          "fusionPrefixes": ["A"],
          "fusionSuffixes": ["b"],
          "staticLastNames": ["L"],
          "legendaryRoots": { "male": ["Z"], "female": ["A"] },
          "legendaryTitles": ["T"],
          "corporateTitles": ["C"],
          "titleTiers": {
            "rank":     { "mundane": ["r"], "silly": ["s"], "highStatus": ["H"] },
            "domain":   { "mundane": ["d"], "silly": ["s"], "highStatus": ["D"] },
            "function": { "mundane": ["f"], "silly": ["s"], "highStatus": ["F"] }
          }
        }
        """;
        var tmp = Path.Combine(Path.GetTempPath(), $"name-data-{Guid.NewGuid():N}.json");
        File.WriteAllText(tmp, json);
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => CastNameDataLoader.Load(tmp));
            Assert.Contains("legendaryRoots", ex.Message);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void Load_MissingBadgeFlair_DoesNotFail()
    {
        // badgeFlair + departmentStamps are optional — loader must accept their absence.
        var json = """
        {
          "schemaVersion": "0.1.0",
          "firstNames": { "male": ["X"], "female": ["Y"], "neutral": ["Z"] },
          "fusionPrefixes": ["A"],
          "fusionSuffixes": ["b"],
          "staticLastNames": ["L"],
          "legendaryRoots": { "male": ["Z"], "female": ["A"], "neutral": ["N"] },
          "legendaryTitles": ["T"],
          "corporateTitles": ["C"],
          "titleTiers": {
            "rank":     { "mundane": ["r"], "silly": ["s"], "highStatus": ["H"] },
            "domain":   { "mundane": ["d"], "silly": ["s"], "highStatus": ["D"] },
            "function": { "mundane": ["f"], "silly": ["s"], "highStatus": ["F"] }
          }
        }
        """;
        var tmp = Path.Combine(Path.GetTempPath(), $"name-data-{Guid.NewGuid():N}.json");
        File.WriteAllText(tmp, json);
        try
        {
            var data = CastNameDataLoader.Load(tmp);
            Assert.Null(data.BadgeFlair);
            Assert.Null(data.DepartmentStamps);
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
