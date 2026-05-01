using APIFramework.Components;
using APIFramework.Systems.Tuning;
using Xunit;

namespace APIFramework.Tests.Systems.Tuning;

/// <summary>AT-01/AT-02/AT-03: archetype-chore-acceptance-bias.json loads cleanly; all 10 archetypes present; biases in 0..1.</summary>
public class ChoreAcceptanceBiasJsonTests
{
    private static readonly string[] AllArchetypes =
    {
        "the-vent", "the-hermit", "the-climber", "the-cynic", "the-newbie",
        "the-old-hand", "the-affair", "the-recovering", "the-founders-nephew", "the-crush",
    };

    private static readonly ChoreKind[] AllChoreKinds = (ChoreKind[])System.Enum.GetValues(typeof(ChoreKind));

    [Fact]
    public void AllTenArchetypesPresent()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        foreach (var archetype in AllArchetypes)
        {
            float bias = cat.GetChoreAcceptanceBias(archetype, ChoreKind.CleanMicrowave);
            Assert.True(bias > 0f && bias <= 1f,
                $"Archetype '{archetype}' cleanMicrowave bias should be > 0 and <= 1 (got {bias})");
        }
    }

    [Fact]
    public void AllBiases_InValidRange()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        foreach (var archetype in AllArchetypes)
            foreach (var kind in AllChoreKinds)
            {
                float bias = cat.GetChoreAcceptanceBias(archetype, kind);
                Assert.True(bias >= 0f && bias <= 1f,
                    $"'{archetype}'.{kind} bias {bias} out of 0..1");
            }
    }

    [Fact]
    public void FoundersNephew_HasVeryLowBias()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        float bias = cat.GetChoreAcceptanceBias("the-founders-nephew", ChoreKind.CleanMicrowave);
        Assert.True(bias < 0.20f, $"Founder's nephew bias should be very low (got {bias})");
    }

    [Fact]
    public void Newbie_HasHigherBias_ThanFoundersNephew()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        float newbie = cat.GetChoreAcceptanceBias("the-newbie", ChoreKind.CleanMicrowave);
        float nephew = cat.GetChoreAcceptanceBias("the-founders-nephew", ChoreKind.CleanMicrowave);
        Assert.True(newbie > nephew,
            "Newbie should have higher chore acceptance than Founder's Nephew");
    }

    [Fact]
    public void MissingArchetype_ReturnsFallback()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        float bias = cat.GetChoreAcceptanceBias("unknown-archetype", ChoreKind.TakeOutTrash);
        Assert.Equal(0.50f, bias);
    }
}
