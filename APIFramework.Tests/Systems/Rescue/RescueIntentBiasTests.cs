using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Rescue;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.Rescue;

/// <summary>
/// Verifies that archetype rescue-bias drives intent correctly for all archetypes
/// defined in the spec, including edge cases around the threshold boundary.
/// </summary>
public class RescueIntentBiasTests
{
    private static RescueConfig Cfg() => new()
    {
        RescueThreshold         = 0.40f,
        AwarenessRangeForRescue = 3.0f,
        MinRescueWillpower      = 20,
        MaxRescueStress         = 80f,
    };

    private static ArchetypeRescueBiasCatalog CatalogWith(string archetype, float bias)
    {
        var path = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(path, $@"{{
            ""schemaVersion"": ""0.1.0"",
            ""archetypeRescueBias"": [
                {{ ""archetype"": ""{archetype}"", ""bias"": {bias.ToString(System.Globalization.CultureInfo.InvariantCulture)},
                   ""heimlichCompetence"": 0.0, ""cprCompetence"": 0.0, ""doorUnlockCompetence"": 0.0 }}
            ]
        }}");
        return ArchetypeRescueBiasCatalog.LoadFromFile(path);
    }

    private static bool HasRescueIntent(string archetype, float bias,
        int willpower = 60, int acuteStress = 0, float distance = 0f)
    {
        var em = new EntityManager();

        var rescuer = em.CreateEntity();
        rescuer.Add(new NpcTag());
        rescuer.Add(new LifeStateComponent { State = LS.Alive });
        rescuer.Add(new PositionComponent { X = 0f, Z = 0f });
        rescuer.Add(new WillpowerComponent(willpower, willpower));
        rescuer.Add(new StressComponent { AcuteLevel = acuteStress });
        rescuer.Add(new NpcArchetypeComponent { ArchetypeId = archetype });

        var victim = em.CreateEntity();
        victim.Add(new NpcTag());
        victim.Add(new LifeStateComponent { State = LS.Incapacitated });
        victim.Add(new PositionComponent { X = distance, Z = 0f });

        var sys = new RescueIntentSystem(CatalogWith(archetype, bias), Cfg());
        sys.Update(em, 1f);

        return rescuer.Has<IntendedActionComponent>() &&
               rescuer.Get<IntendedActionComponent>().Kind == IntendedActionKind.Rescue;
    }

    // High-bias archetypes should rescue
    [Theory]
    [InlineData("the-newbie",    0.85f)]
    [InlineData("the-old-hand",  0.80f)]
    [InlineData("the-recovering",0.65f)]
    [InlineData("the-cynic",     0.55f)]
    public void HighBiasArchetypes_ShouldRescue(string archetype, float bias)
    {
        Assert.True(HasRescueIntent(archetype, bias, willpower: 60),
            $"{archetype} (bias={bias}) should emit rescue intent");
    }

    // Low-bias archetype at mid willpower falls below threshold
    [Fact]
    public void FoundersNephew_MidWillpower_NoRescue()
    {
        // bias=0.10, willpower=60 → score = 0.10 + (60/100)*0.3 = 0.28 < 0.40
        Assert.False(HasRescueIntent("the-founders-nephew", 0.10f, willpower: 60));
    }

    // Hermit at zero distance and max willpower: 0.30 + 0.30 = 0.60 > 0.40
    [Fact]
    public void Hermit_MaxWillpower_Rescues()
    {
        Assert.True(HasRescueIntent("the-hermit", 0.30f, willpower: 100));
    }

    // Distance penalty reduces score
    [Fact]
    public void LargeDistance_ReducesScoreBelowThreshold()
    {
        // hermit bias=0.30, willpower=60 → base 0.30 + 0.18 = 0.48
        // at distance=2.0: penalty 2.0*0.05 = 0.10 → score 0.38 < 0.40
        Assert.False(HasRescueIntent("the-hermit", 0.30f, willpower: 60, distance: 2.0f));
    }
}
