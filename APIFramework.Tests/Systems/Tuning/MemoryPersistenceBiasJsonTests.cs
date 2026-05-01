using APIFramework.Systems.Tuning;
using Xunit;

namespace APIFramework.Tests.Systems.Tuning;

/// <summary>AT-01/AT-02/AT-03: archetype-memory-persistence-bias.json loads cleanly; all 10 archetypes present; multipliers in valid range.</summary>
public class MemoryPersistenceBiasJsonTests
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
            var bias = cat.GetMemoryPersistenceBias(archetype);
            Assert.True(bias.PersistenceMult > 0f,
                $"Archetype '{archetype}' should have a memory persistence bias entry");
        }
    }

    [Fact]
    public void AllMultipliers_InValidRange()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        foreach (var archetype in AllArchetypes)
        {
            var bias = cat.GetMemoryPersistenceBias(archetype);
            Assert.True(bias.PersistenceMult is >= 0.5f and <= 2.0f,
                $"'{archetype}' persistenceMult {bias.PersistenceMult} out of 0.5..2.0");
            Assert.True(bias.DecayRateMult is >= 0.5f and <= 2.0f,
                $"'{archetype}' decayRateMult {bias.DecayRateMult} out of 0.5..2.0");
        }
    }

    [Fact]
    public void Cynic_HasLowerPersistence_AndHigherDecay_ThanOldHand()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        var cynic   = cat.GetMemoryPersistenceBias("the-cynic");
        var oldHand = cat.GetMemoryPersistenceBias("the-old-hand");
        Assert.True(cynic.PersistenceMult < oldHand.PersistenceMult,
            "Cynic should have lower persistenceMult than Old Hand");
        Assert.True(cynic.DecayRateMult > oldHand.DecayRateMult,
            "Cynic should have higher decayRateMult than Old Hand (memories fade faster)");
    }

    [Fact]
    public void KnownValues_MatchDesignSpec()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        var cynic = cat.GetMemoryPersistenceBias("the-cynic");
        Assert.Equal(0.85f, cynic.PersistenceMult, precision: 3);
        Assert.Equal(1.20f, cynic.DecayRateMult, precision: 3);

        var oldHand = cat.GetMemoryPersistenceBias("the-old-hand");
        Assert.Equal(1.10f, oldHand.PersistenceMult, precision: 3);
        Assert.Equal(0.85f, oldHand.DecayRateMult, precision: 3);
    }

    [Fact]
    public void MissingArchetype_ReturnsDefault()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        var bias = cat.GetMemoryPersistenceBias("unknown");
        Assert.Equal(MemoryPersistenceBias.Default, bias);
    }
}
