using System.Collections.Generic;
using System.IO;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Data;

/// <summary>AT-11: archetype-workload-capacity.json loads cleanly; all 10 archetypes present; capacities in 1..10.</summary>
public class ArchetypeWorkloadCapacityJsonTests
{
    private static IReadOnlyDictionary<string, int> LoadCapacities()
    {
        var path = WorkloadInitializerSystem.FindDefaultPath();
        Assert.True(path is not null && File.Exists(path),
            "archetype-workload-capacity.json not found via FindDefaultPath() — check file location");
        return WorkloadInitializerSystem.LoadCapacities(path);
    }

    [Fact]
    public void File_LoadsWithoutException()
    {
        var caps = LoadCapacities();
        Assert.NotNull(caps);
    }

    [Fact]
    public void AllTenArchetypesPresent()
    {
        var caps = LoadCapacities();

        var expected = new[]
        {
            "the-vent", "the-hermit", "the-climber", "the-cynic", "the-newbie",
            "the-old-hand", "the-affair", "the-recovering", "the-founders-nephew", "the-crush",
        };

        Assert.Equal(10, caps.Count);
        foreach (var id in expected)
            Assert.True(caps.ContainsKey(id), $"Missing archetype '{id}' in capacities file");
    }

    [Fact]
    public void AllCapacities_InRange_1_To_10()
    {
        var caps = LoadCapacities();
        foreach (var (id, capacity) in caps)
            Assert.True(capacity >= 1 && capacity <= 10,
                $"Archetype '{id}' capacity {capacity} is outside 1..10");
    }

    [Fact]
    public void KnownValues_MatchDesignSpec()
    {
        var caps = LoadCapacities();

        Assert.Equal(5, caps["the-climber"]);
        Assert.Equal(1, caps["the-founders-nephew"]);
        Assert.Equal(2, caps["the-hermit"]);
        Assert.Equal(4, caps["the-old-hand"]);
        Assert.Equal(2, caps["the-newbie"]);
        Assert.Equal(2, caps["the-recovering"]);
        Assert.Equal(3, caps["the-crush"]);
    }

    [Fact]
    public void LoadCapacities_MissingFile_ReturnsEmptyDictionary()
    {
        var caps = WorkloadInitializerSystem.LoadCapacities("/nonexistent/path/file.json");
        Assert.Empty(caps);
    }

    [Fact]
    public void LoadCapacities_IsCaseInsensitive()
    {
        var caps = LoadCapacities();
        Assert.True(caps.ContainsKey("THE-CLIMBER"),    "Key lookup should be case-insensitive (upper)");
        Assert.True(caps.ContainsKey("The-Climber"),    "Key lookup should be case-insensitive (title)");
        Assert.True(caps.ContainsKey("the-climber"),    "Key lookup should be case-insensitive (lower)");
    }
}
