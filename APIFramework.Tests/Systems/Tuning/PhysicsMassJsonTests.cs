using APIFramework.Systems.Tuning;
using Xunit;

namespace APIFramework.Tests.Systems.Tuning;

/// <summary>AT-01/AT-02/AT-03: archetype-physics-mass.json loads cleanly; all 10 archetypes present; masses in 50..100 kg.</summary>
public class PhysicsMassJsonTests
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
            float mass = cat.GetMassKg(archetype);
            Assert.True(mass > 0f, $"Archetype '{archetype}' should have a mass > 0 (got {mass})");
        }
    }

    [Fact]
    public void AllMasses_InValidRange_50To100kg()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        foreach (var archetype in AllArchetypes)
        {
            float mass = cat.GetMassKg(archetype);
            Assert.True(mass is >= 50f and <= 100f,
                $"'{archetype}' mass {mass} kg is outside 50..100 kg");
        }
    }

    [Fact]
    public void KnownMasses_MatchDesignSpec()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        Assert.Equal(75f, cat.GetMassKg("the-old-hand"));
        Assert.Equal(65f, cat.GetMassKg("the-newbie"));
        Assert.Equal(60f, cat.GetMassKg("the-crush"));
        Assert.Equal(80f, cat.GetMassKg("the-vent"));
    }

    [Fact]
    public void MissingArchetype_ReturnsDefaultMass()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        float mass = cat.GetMassKg("unknown-archetype");
        Assert.Equal(70f, mass);
    }
}
