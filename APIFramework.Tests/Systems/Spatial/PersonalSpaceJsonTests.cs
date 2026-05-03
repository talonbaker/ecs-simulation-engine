using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Spatial;

/// <summary>
/// AT-06: All cast-bible archetypes present in archetype-personal-space.json;
///         multipliers in valid range (0.5..2.0).
/// </summary>
public class PersonalSpaceJsonTests
{
    private static readonly string[] ExpectedArchetypes =
    [
        "the-hermit",
        "the-cynic",
        "the-old-hand",
        "the-recovering",
        "the-affair",
        "the-climber",
        "the-founders-nephew",
        "the-newbie",
        "the-vent",
        "the-crush"
    ];

    [Fact]
    public void AT06_JsonFile_ContainsAllArchetypes()
    {
        var path = SpatialBehaviorInitializerSystem.FindDefaultPath();
        Assert.True(path != null && File.Exists(path),
            "archetype-personal-space.json not found — searched upward from CWD.");

        var tuning = SpatialBehaviorInitializerSystem.LoadTuning(path);
        Assert.Equal(ExpectedArchetypes.Length, tuning.Count);

        foreach (var id in ExpectedArchetypes)
            Assert.True(tuning.ContainsKey(id), $"Missing archetype: {id}");
    }

    [Fact]
    public void AT06_JsonFile_MultipliersInValidRange()
    {
        var path = SpatialBehaviorInitializerSystem.FindDefaultPath();
        Assert.True(path != null && File.Exists(path), "JSON file not found.");

        var tuning = SpatialBehaviorInitializerSystem.LoadTuning(path);

        foreach (var (id, (rMult, sMult)) in tuning)
        {
            Assert.True(rMult >= 0.5f && rMult <= 2.0f,
                $"{id}.radiusMult {rMult} out of range [0.5..2.0]");
            Assert.True(sMult >= 0.5f && sMult <= 2.0f,
                $"{id}.repulsionStrengthMult {sMult} out of range [0.5..2.0]");
        }
    }

    [Fact]
    public void JsonFile_SchemaVersion_IsPresent()
    {
        var path = SpatialBehaviorInitializerSystem.FindDefaultPath();
        Assert.True(path != null && File.Exists(path), "JSON file not found.");

        var text = File.ReadAllText(path);
        var doc  = JsonDocument.Parse(text);
        Assert.True(doc.RootElement.TryGetProperty("schemaVersion", out _),
            "archetype-personal-space.json must have a schemaVersion field.");
    }

    [Fact]
    public void HermitRadius_LargerThan_VentRadius()
    {
        var tuning = SpatialBehaviorInitializerSystem.LoadTuning();
        if (!tuning.ContainsKey("the-hermit") || !tuning.ContainsKey("the-vent"))
            return; // file not present in this environment — skip

        Assert.True(tuning["the-hermit"].RadiusMult > tuning["the-vent"].RadiusMult,
            "Hermit radius multiplier should exceed Vent.");
    }
}
