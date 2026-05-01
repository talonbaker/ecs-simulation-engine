using System.IO;
using APIFramework.Systems.Tuning;
using Xunit;

namespace APIFramework.Tests.Systems.Tuning;

/// <summary>AT-01/AT-02/AT-03: archetype-choke-bias.json loads cleanly; all 10 archetypes present; multipliers in valid range.</summary>
public class ChokeBiasJsonTests
{
    private static readonly string[] AllArchetypes =
    {
        "the-vent", "the-hermit", "the-climber", "the-cynic", "the-newbie",
        "the-old-hand", "the-affair", "the-recovering", "the-founders-nephew", "the-crush",
    };

    private static TuningCatalog LoadCatalog()
    {
        var catalog = TuningCatalog.LoadFromDirectory();
        return catalog;
    }

    [Fact]
    public void File_LoadsWithoutException()
    {
        var cat = LoadCatalog();
        Assert.NotNull(cat);
    }

    [Fact]
    public void AllTenArchetypesPresent()
    {
        var cat = LoadCatalog();
        foreach (var archetype in AllArchetypes)
        {
            var bias = cat.GetChokeBias(archetype);
            Assert.True(bias.BolusSizeThresholdMult > 0f,
                $"Archetype '{archetype}' should have a choke bias entry (bolusSizeThresholdMult > 0)");
        }
    }

    [Fact]
    public void AllMultipliers_InValidRange()
    {
        var cat = LoadCatalog();
        foreach (var archetype in AllArchetypes)
        {
            var bias = cat.GetChokeBias(archetype);
            Assert.True(bias.BolusSizeThresholdMult is >= 0.5f and <= 2.0f,
                $"'{archetype}' bolusSizeThresholdMult {bias.BolusSizeThresholdMult} out of 0.5..2.0");
            Assert.True(bias.EnergyThresholdMult is >= 0.5f and <= 2.0f,
                $"'{archetype}' energyThresholdMult {bias.EnergyThresholdMult} out of 0.5..2.0");
            Assert.True(bias.StressThresholdMult is >= 0.5f and <= 2.0f,
                $"'{archetype}' stressThresholdMult {bias.StressThresholdMult} out of 0.5..2.0");
        }
    }

    [Fact]
    public void Newbie_HasLowerBolusSizeThreshold_ThanOldHand()
    {
        var cat = LoadCatalog();
        var newbie  = cat.GetChokeBias("the-newbie");
        var oldHand = cat.GetChokeBias("the-old-hand");
        Assert.True(newbie.BolusSizeThresholdMult < oldHand.BolusSizeThresholdMult,
            "Newbie should have a lower bolus threshold multiplier than Old Hand (chokes more easily)");
    }

    [Fact]
    public void MissingArchetype_ReturnsDefault()
    {
        var cat = LoadCatalog();
        var bias = cat.GetChokeBias("non-existent-archetype");
        Assert.Equal(ChokeBias.Default, bias);
    }

    [Fact]
    public void NullArchetype_ReturnsDefault()
    {
        var cat = LoadCatalog();
        var bias = cat.GetChokeBias(null);
        Assert.Equal(ChokeBias.Default, bias);
    }
}
