using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Data;

/// <summary>
/// AT-10: Validates the archetype-schedules.json data file:
///  — all 10 archetypes are present
///  — every block's anchorId is one of the 6 anchor tags present in office-starter.json
///  — each archetype's blocks cover the full 24-hour day (sampled every 30 min)
///  — no two blocks within the same archetype are active at the same sample point
/// </summary>
public class ArchetypeSchedulesJsonValidationTests
{
    private static readonly HashSet<string> ValidAnchorTags = new(System.StringComparer.Ordinal)
    {
        "the-microwave",
        "the-window",
        "the-it-closet",
        "the-supply-closet",
        "the-conference-room",
        "the-parking-lot",
    };

    private static readonly string[] ExpectedArchetypes =
    {
        "the-vent",
        "the-hermit",
        "the-climber",
        "the-cynic",
        "the-newbie",
        "the-old-hand",
        "the-affair",
        "the-recovering",
        "the-founders-nephew",
        "the-crush",
    };

    private static Dictionary<string, IReadOnlyList<ScheduleBlock>> LoadData()
    {
        var path = ScheduleSpawnerSystem.FindSchedulesFile();
        Assert.NotNull(path);
        return ScheduleSpawnerSystem.LoadSchedules(path!);
    }

    [Fact]
    public void AT10_AllTenArchetypesPresent()
    {
        var data = LoadData();
        foreach (var id in ExpectedArchetypes)
            Assert.True(data.ContainsKey(id), $"Archetype '{id}' missing from archetype-schedules.json");
        Assert.Equal(ExpectedArchetypes.Length, data.Count);
    }

    [Fact]
    public void AT10_AllAnchorIdsAreValid()
    {
        var data = LoadData();
        foreach (var (archetypeId, blocks) in data)
        {
            foreach (var block in blocks)
            {
                Assert.True(
                    ValidAnchorTags.Contains(block.AnchorId),
                    $"Archetype '{archetypeId}' block [{block.StartHour}→{block.EndHour}] " +
                    $"uses unknown anchorId '{block.AnchorId}'");
            }
        }
    }

    [Fact]
    public void AT10_EachArchetype_CoversFullDay_NoOverlaps()
    {
        var data = LoadData();

        // Sample every 30 minutes: 0.0, 0.5, 1.0, … 23.5
        var samplePoints = Enumerable.Range(0, 48).Select(i => i * 0.5f).ToArray();

        foreach (var (archetypeId, blocks) in data)
        {
            foreach (float hour in samplePoints)
            {
                int activeCount = blocks.Count(b => ScheduleSystem.IsBlockActive(b.StartHour, b.EndHour, hour));

                Assert.True(activeCount >= 1,
                    $"Archetype '{archetypeId}': gap at {hour:F1}h — no block active");

                Assert.True(activeCount <= 1,
                    $"Archetype '{archetypeId}': overlap at {hour:F1}h — {activeCount} blocks active");
            }
        }
    }
}
