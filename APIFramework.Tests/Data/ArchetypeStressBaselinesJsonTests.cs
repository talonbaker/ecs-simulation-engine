using System.Collections.Generic;
using System.IO;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Data;

/// <summary>AT-12: archetype-stress-baselines.json loads cleanly; all 10 archetypes present; baselines in 0..100.</summary>
public class ArchetypeStressBaselinesJsonTests
{
    private static IReadOnlyDictionary<string, double> LoadBaselines()
    {
        // Walk up from the test binary location to find the JSON file
        var path = StressInitializerSystem.FindDefaultPath();
        Assert.True(path is not null && File.Exists(path),
            "archetype-stress-baselines.json not found via FindDefaultPath() — check file location");
        return StressInitializerSystem.LoadBaselines(path);
    }

    [Fact]
    public void File_LoadsWithoutException()
    {
        var baselines = LoadBaselines();
        Assert.NotNull(baselines);
    }

    [Fact]
    public void AllTenArchetypesPresent()
    {
        var baselines = LoadBaselines();

        var expected = new[]
        {
            "the-cynic", "the-vent", "the-recovering", "the-hermit", "the-climber",
            "the-newbie", "the-old-hand", "the-affair", "the-founders-nephew", "the-crush",
        };

        Assert.Equal(10, baselines.Count);
        foreach (var id in expected)
            Assert.True(baselines.ContainsKey(id), $"Missing archetype '{id}' in baselines file");
    }

    [Fact]
    public void AllBaselines_InRange_0_To_100()
    {
        var baselines = LoadBaselines();

        foreach (var (id, level) in baselines)
            Assert.True(level >= 0 && level <= 100,
                $"Archetype '{id}' chronicLevel {level} is outside 0..100");
    }

    [Fact]
    public void KnownValues_MatchDesignSpec()
    {
        var baselines = LoadBaselines();

        Assert.Equal(20.0,  baselines["the-cynic"]);
        Assert.Equal(50.0,  baselines["the-vent"]);
        Assert.Equal(60.0,  baselines["the-recovering"]);
        Assert.Equal(30.0,  baselines["the-hermit"]);
        Assert.Equal(50.0,  baselines["the-climber"]);
        Assert.Equal(40.0,  baselines["the-newbie"]);
        Assert.Equal(30.0,  baselines["the-old-hand"]);
        Assert.Equal(60.0,  baselines["the-affair"]);
        Assert.Equal(25.0,  baselines["the-founders-nephew"]);
        Assert.Equal(35.0,  baselines["the-crush"]);
    }

    [Fact]
    public void LoadBaselines_MissingFile_ReturnsEmptyDictionary()
    {
        var baselines = StressInitializerSystem.LoadBaselines("/nonexistent/path/file.json");
        Assert.Empty(baselines);
    }

    [Fact]
    public void LoadBaselines_IsCaseInsensitive()
    {
        var baselines = LoadBaselines();

        Assert.True(baselines.ContainsKey("THE-CYNIC"),    "Key lookup should be case-insensitive (upper)");
        Assert.True(baselines.ContainsKey("The-Cynic"),    "Key lookup should be case-insensitive (title)");
        Assert.True(baselines.ContainsKey("the-cynic"),    "Key lookup should be case-insensitive (lower)");
    }
}
