using APIFramework.Systems.Tuning;
using Xunit;

namespace APIFramework.Tests.Systems.Tuning;

/// <summary>AT-01/AT-02/AT-03: archetype-bereavement-bias.json loads cleanly; all 10 archetypes present; multipliers in valid range.</summary>
public class BereavementBiasJsonTests
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
            var bias = cat.GetBereavementBias(archetype);
            Assert.True(bias.MoodIntensityMult > 0f,
                $"Archetype '{archetype}' should have a bereavement bias entry");
        }
    }

    [Fact]
    public void AllMultipliers_InValidRange()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        foreach (var archetype in AllArchetypes)
        {
            var bias = cat.GetBereavementBias(archetype);
            Assert.True(bias.StressIntensityMult is >= 0.5f and <= 2.0f,
                $"'{archetype}' stressIntensityMult {bias.StressIntensityMult} out of range");
            Assert.True(bias.MoodIntensityMult is >= 0.5f and <= 2.0f,
                $"'{archetype}' moodIntensityMult {bias.MoodIntensityMult} out of range");
            Assert.True(bias.MemoryPersistenceMult is >= 0.5f and <= 2.0f,
                $"'{archetype}' memoryPersistenceMult {bias.MemoryPersistenceMult} out of range");
        }
    }

    [Fact]
    public void Vent_GrievesHarder_ThanCynic()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        var vent  = cat.GetBereavementBias("the-vent");
        var cynic = cat.GetBereavementBias("the-cynic");
        Assert.True(vent.MoodIntensityMult > cynic.MoodIntensityMult,
            "Vent should have higher moodIntensityMult than Cynic");
        Assert.True(vent.StressIntensityMult > cynic.StressIntensityMult,
            "Vent should have higher stressIntensityMult than Cynic");
    }

    [Fact]
    public void MissingArchetype_ReturnsDefault()
    {
        var cat = TuningCatalog.LoadFromDirectory();
        var bias = cat.GetBereavementBias("unknown");
        Assert.Equal(BereavementBias.Default, bias);
    }
}
