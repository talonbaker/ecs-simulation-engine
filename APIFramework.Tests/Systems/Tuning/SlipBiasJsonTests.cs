using APIFramework.Systems.Tuning;
using Xunit;

namespace APIFramework.Tests.Systems.Tuning;

/// <summary>AT-01/AT-02/AT-03: archetype-slip-bias.json loads cleanly; all 10 archetypes present; multipliers in valid range.</summary>
public class SlipBiasJsonTests
{
    private static readonly string[] AllArchetypes =
    {
        "the-vent", "the-hermit", "the-climber", "the-cynic", "the-newbie",
        "the-old-hand", "the-affair", "the-recovering", "the-founders-nephew", "the-crush",
    };

    [Fact]
    public void AllTenArchetypesPresent()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        foreach (var archetype in AllArchetypes)
        {
            var bias = cat.GetSlipBias(archetype);
            Assert.True(bias.SlipChanceMult > 0f,
                $"Archetype '{archetype}' should have a slip bias entry (slipChanceMult > 0)");
        }
    }

    [Fact]
    public void AllMultipliers_InValidRange()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        foreach (var archetype in AllArchetypes)
        {
            var bias = cat.GetSlipBias(archetype);
            Assert.True(bias.MovementSpeedFactor is >= 0.5f and <= 2.0f,
                $"'{archetype}' movementSpeedFactor {bias.MovementSpeedFactor} out of 0.5..2.0");
            Assert.True(bias.SlipChanceMult is >= 0.5f and <= 2.0f,
                $"'{archetype}' slipChanceMult {bias.SlipChanceMult} out of 0.5..2.0");
        }
    }

    [Fact]
    public void OldHand_HasLowerSlipChance_ThanNewbie()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        var oldHand = cat.GetSlipBias("the-old-hand");
        var newbie  = cat.GetSlipBias("the-newbie");
        Assert.True(oldHand.SlipChanceMult < newbie.SlipChanceMult,
            "Old Hand should have a lower slip chance multiplier than Newbie");
    }

    [Fact]
    public void OldHand_HasLowerMovementSpeed_ThanNewbie()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        var oldHand = cat.GetSlipBias("the-old-hand");
        var newbie  = cat.GetSlipBias("the-newbie");
        Assert.True(oldHand.MovementSpeedFactor < newbie.MovementSpeedFactor,
            "Old Hand should have a lower movement speed factor than Newbie (moves more carefully)");
    }

    [Fact]
    public void MissingArchetype_ReturnsDefault()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        var bias = cat.GetSlipBias("non-existent");
        Assert.Equal(SlipBias.Default, bias);
    }
}
