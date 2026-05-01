using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Systems.Chores;
using Xunit;

namespace APIFramework.Tests.Systems.Chores;

/// <summary>AT-12: chore-archetype-acceptance-bias.json loads correctly; all cast-bible archetypes covered.</summary>
public class ChoreAcceptanceBiasJsonTests
{
    private static readonly string[] ExpectedArchetypes =
    {
        "the-old-hand",
        "the-cynic",
        "the-newbie",
        "the-founders-nephew",
        "the-climber",
        "the-recovering",
        "the-vent",
        "the-hermit",
    };

    [Fact]
    public void ParseJson_LoadsBiasesCorrectly()
    {
        const string json = @"{
            ""schemaVersion"": ""0.1.0"",
            ""biases"": {
                ""the-newbie"": { ""cleanMicrowave"": 0.95, ""cleanFridge"": 0.90 }
            }
        }";

        var table = ChoreAcceptanceBiasTable.ParseJson(json);

        Assert.Equal(0.95f, table.GetBias("the-newbie", ChoreKind.CleanMicrowave));
        Assert.Equal(0.90f, table.GetBias("the-newbie", ChoreKind.CleanFridge));
    }

    [Fact]
    public void ParseJson_FallsBackToDefaultBias_ForMissingArchetype()
    {
        const string json = @"{ ""biases"": {} }";
        var table = ChoreAcceptanceBiasTable.ParseJson(json, defaultBias: 0.50f);

        Assert.Equal(0.50f, table.GetBias("unknown-archetype", ChoreKind.CleanMicrowave));
    }

    [Fact]
    public void ParseJson_FallsBackToDefaultBias_ForMissingChoreKind()
    {
        const string json = @"{
            ""biases"": {
                ""the-newbie"": { ""cleanMicrowave"": 0.95 }
            }
        }";
        var table = ChoreAcceptanceBiasTable.ParseJson(json, defaultBias: 0.50f);

        // CleanFridge not listed for the-newbie → falls back to default
        Assert.Equal(0.50f, table.GetBias("the-newbie", ChoreKind.CleanFridge));
    }

    [Fact]
    public void ParseJson_IsCaseInsensitive_ForChoreNames()
    {
        // camelCase chore names in JSON map correctly to ChoreKind enum
        const string json = @"{
            ""biases"": {
                ""test-archetype"": {
                    ""cleanMicrowave"":      0.10,
                    ""cleanFridge"":         0.20,
                    ""cleanBathroom"":       0.30,
                    ""takeOutTrash"":        0.40,
                    ""refillWaterCooler"":   0.50,
                    ""restockSupplyCloset"": 0.60,
                    ""replaceToner"":        0.70
                }
            }
        }";

        var table = ChoreAcceptanceBiasTable.ParseJson(json);

        Assert.Equal(0.10f, table.GetBias("test-archetype", ChoreKind.CleanMicrowave));
        Assert.Equal(0.20f, table.GetBias("test-archetype", ChoreKind.CleanFridge));
        Assert.Equal(0.30f, table.GetBias("test-archetype", ChoreKind.CleanBathroom));
        Assert.Equal(0.40f, table.GetBias("test-archetype", ChoreKind.TakeOutTrash));
        Assert.Equal(0.50f, table.GetBias("test-archetype", ChoreKind.RefillWaterCooler));
        Assert.Equal(0.60f, table.GetBias("test-archetype", ChoreKind.RestockSupplyCloset));
        Assert.Equal(0.70f, table.GetBias("test-archetype", ChoreKind.ReplaceToner));
    }

    [Fact]
    public void LoadDefault_LoadsAllCastBibleArchetypes()
    {
        var table = ChoreAcceptanceBiasTable.LoadDefault();

        foreach (var archetype in ExpectedArchetypes)
        {
            float bias = table.GetBias(archetype, ChoreKind.CleanMicrowave);
            Assert.True(bias > 0.0f && bias <= 1.0f,
                $"Archetype '{archetype}' should have a cleanMicrowave bias > 0 and <= 1 (got {bias})");
        }
    }

    [Fact]
    public void LoadDefault_FoundersNephew_HasVeryLowBias()
    {
        var table = ChoreAcceptanceBiasTable.LoadDefault();
        float bias = table.GetBias("the-founders-nephew", ChoreKind.CleanMicrowave);
        Assert.True(bias < 0.20f, $"Founder's nephew bias should be below minAcceptance threshold (got {bias})");
    }

    [Fact]
    public void LoadDefault_Newbie_HasHighBias()
    {
        var table = ChoreAcceptanceBiasTable.LoadDefault();
        float bias = table.GetBias("the-newbie", ChoreKind.CleanMicrowave);
        Assert.True(bias > 0.80f, $"The newbie bias for cleanMicrowave should be high (got {bias})");
    }

    [Fact]
    public void ParseJson_EmptyBiasObject_ReturnsDefaultForAll()
    {
        var table = ChoreAcceptanceBiasTable.ParseJson("{}", defaultBias: 0.42f);

        foreach (ChoreKind kind in Enum.GetValues<ChoreKind>())
            Assert.Equal(0.42f, table.GetBias("any-archetype", kind));
    }
}
