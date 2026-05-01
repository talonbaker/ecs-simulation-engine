using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Rescue;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.Rescue;

/// <summary>
/// AT-08: Low-willpower rescuer (WillpowerComponent.Current &lt; MinRescueWillpower) → no intent emitted.
/// </summary>
public class RescueLowWillpowerTests
{
    private static RescueConfig Cfg() => new()
    {
        RescueThreshold         = 0.40f,
        AwarenessRangeForRescue = 3.0f,
        MinRescueWillpower      = 20,
        MaxRescueStress         = 80f,
    };

    private static ArchetypeRescueBiasCatalog HighBiasCatalog()
    {
        var path = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(path, @"{
            ""schemaVersion"": ""0.1.0"",
            ""archetypeRescueBias"": [
                { ""archetype"": ""the-newbie"", ""bias"": 0.85,
                  ""heimlichCompetence"": 0.1, ""cprCompetence"": 0.05, ""doorUnlockCompetence"": 0.03 }
            ]
        }");
        return ArchetypeRescueBiasCatalog.LoadFromFile(path);
    }

    // ── AT-08: willpower below gate → no intent ──────────────────────────────

    [Fact]
    public void WillpowerBelowMinGate_NoRescueIntent()
    {
        var em = new EntityManager();

        var rescuer = em.CreateEntity();
        rescuer.Add(new NpcTag());
        rescuer.Add(new LifeStateComponent { State = LS.Alive });
        rescuer.Add(new PositionComponent { X = 0f, Z = 0f });
        rescuer.Add(new WillpowerComponent(10, 50)); // Current=10, below MinRescueWillpower=20
        rescuer.Add(new StressComponent { AcuteLevel = 0 });
        rescuer.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });

        var victim = em.CreateEntity();
        victim.Add(new NpcTag());
        victim.Add(new LifeStateComponent { State = LS.Incapacitated });
        victim.Add(new PositionComponent { X = 1f, Z = 0f });

        var sys = new RescueIntentSystem(HighBiasCatalog(), Cfg());
        sys.Update(em, 1f);

        if (rescuer.Has<IntendedActionComponent>())
            Assert.NotEqual(IntendedActionKind.Rescue, rescuer.Get<IntendedActionComponent>().Kind);
    }

    [Fact]
    public void WillpowerAtMinGate_IsEligible()
    {
        // Exactly at MinRescueWillpower=20 → eligible (>= check)
        var em = new EntityManager();

        var rescuer = em.CreateEntity();
        rescuer.Add(new NpcTag());
        rescuer.Add(new LifeStateComponent { State = LS.Alive });
        rescuer.Add(new PositionComponent { X = 0f, Z = 0f });
        rescuer.Add(new WillpowerComponent(20, 50));
        rescuer.Add(new StressComponent { AcuteLevel = 0 });
        rescuer.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });

        var victim = em.CreateEntity();
        victim.Add(new NpcTag());
        victim.Add(new LifeStateComponent { State = LS.Incapacitated });
        victim.Add(new PositionComponent { X = 1f, Z = 0f });

        var sys = new RescueIntentSystem(HighBiasCatalog(), Cfg());
        sys.Update(em, 1f);

        // With bias=0.85, willpower=20 → score = 0.85 + (20/100)*0.3 = 0.91 > 0.40
        Assert.True(rescuer.Has<IntendedActionComponent>() &&
                    rescuer.Get<IntendedActionComponent>().Kind == IntendedActionKind.Rescue);
    }
}
