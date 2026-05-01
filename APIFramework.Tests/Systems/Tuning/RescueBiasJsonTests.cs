using APIFramework.Systems.Tuning;
using Xunit;

namespace APIFramework.Tests.Systems.Tuning;

/// <summary>AT-01/AT-02/AT-03: archetype-rescue-bias.json loads cleanly; all 10 archetypes present; biases in 0..1.</summary>
public class RescueBiasJsonTests
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
            var bias = cat.GetRescueBias(archetype);
            Assert.True(bias.Bias >= 0f,
                $"Archetype '{archetype}' should have a rescue bias >= 0 (got {bias.Bias})");
        }
    }

    [Fact]
    public void AllBiases_InValidRange()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        foreach (var archetype in AllArchetypes)
        {
            var bias = cat.GetRescueBias(archetype);
            Assert.True(bias.Bias is >= 0f and <= 1f,
                $"'{archetype}' bias {bias.Bias} out of 0..1");
            Assert.True(bias.HeimlichCompetence is >= 0f and <= 1f,
                $"'{archetype}' heimlichCompetence out of 0..1");
            Assert.True(bias.CprCompetence is >= 0f and <= 1f,
                $"'{archetype}' cprCompetence out of 0..1");
            Assert.True(bias.DoorUnlockCompetence is >= 0f and <= 1f,
                $"'{archetype}' doorUnlockCompetence out of 0..1");
        }
    }

    [Fact]
    public void Newbie_HasHigherBias_ThanFoundersNephew()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        var newbie  = cat.GetRescueBias("the-newbie");
        var nephew  = cat.GetRescueBias("the-founders-nephew");
        Assert.True(newbie.Bias > nephew.Bias,
            "Newbie should have higher rescue bias than Founder's Nephew");
    }

    [Fact]
    public void FoundersNephew_HasVeryLowBias()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        var bias = cat.GetRescueBias("the-founders-nephew");
        Assert.True(bias.Bias < 0.20f, $"Founder's Nephew rescue bias should be very low (got {bias.Bias})");
    }

    [Fact]
    public void MissingArchetype_ReturnsDefault()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        var bias = cat.GetRescueBias("unknown");
        Assert.Equal(RescueBias.Default, bias);
    }
}
