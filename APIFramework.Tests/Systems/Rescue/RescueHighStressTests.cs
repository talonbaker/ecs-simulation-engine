using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Rescue;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.Rescue;

/// <summary>
/// AT-09: High-stress rescuer (AcuteStress &gt; MaxRescueStress) → no intent emitted.
/// </summary>
public class RescueHighStressTests
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

    // ── AT-09: stress above gate → no intent ────────────────────────────────

    [Fact]
    public void StressAboveMaxGate_NoRescueIntent()
    {
        var em = new EntityManager();

        var rescuer = em.CreateEntity();
        rescuer.Add(new NpcTag());
        rescuer.Add(new LifeStateComponent { State = LS.Alive });
        rescuer.Add(new PositionComponent { X = 0f, Z = 0f });
        rescuer.Add(new WillpowerComponent(60, 60));
        rescuer.Add(new StressComponent { AcuteLevel = 90 }); // above MaxRescueStress=80
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
    public void StressAtMaxGate_IsEligible()
    {
        // Exactly at MaxRescueStress=80 is borderline. The gate is >, so 80 is still eligible.
        var em = new EntityManager();

        var rescuer = em.CreateEntity();
        rescuer.Add(new NpcTag());
        rescuer.Add(new LifeStateComponent { State = LS.Alive });
        rescuer.Add(new PositionComponent { X = 0f, Z = 0f });
        rescuer.Add(new WillpowerComponent(60, 60));
        rescuer.Add(new StressComponent { AcuteLevel = 80 }); // exactly at MaxRescueStress
        rescuer.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });

        var victim = em.CreateEntity();
        victim.Add(new NpcTag());
        victim.Add(new LifeStateComponent { State = LS.Incapacitated });
        victim.Add(new PositionComponent { X = 1f, Z = 0f });

        var sys = new RescueIntentSystem(HighBiasCatalog(), Cfg());
        sys.Update(em, 1f);

        // score = 0.85 + (60/100)*0.3 - 80*0.005 = 0.85 + 0.18 - 0.40 = 0.63 > 0.40
        Assert.True(rescuer.Has<IntendedActionComponent>() &&
                    rescuer.Get<IntendedActionComponent>().Kind == IntendedActionKind.Rescue);
    }
}
