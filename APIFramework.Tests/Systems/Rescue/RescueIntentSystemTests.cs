using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Rescue;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.Rescue;

/// <summary>
/// AT-02: Alive NPC in awareness range of Incapacitated → RescueIntentSystem emits Rescue intent (above-threshold archetype).
/// AT-03: Below-threshold archetype (FoundersNephew) → no intent emitted.
/// </summary>
public class RescueIntentSystemTests
{
    private static RescueConfig DefaultCfg() => new()
    {
        RescueThreshold        = 0.40f,
        AwarenessRangeForRescue = 3.0f,
        MinRescueWillpower     = 20,
        MaxRescueStress        = 80f,
    };

    private static ArchetypeRescueBiasCatalog MakeCatalog(string archetype, float bias) =>
        ArchetypeRescueBiasCatalog.LoadFromFile(BuildTempJson(archetype, bias));

    private static string BuildTempJson(string archetype, float bias)
    {
        var path = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(path, $@"{{
            ""schemaVersion"": ""0.1.0"",
            ""archetypeRescueBias"": [
                {{ ""archetype"": ""{archetype}"", ""bias"": {bias.ToString(System.Globalization.CultureInfo.InvariantCulture)},
                   ""heimlichCompetence"": 0.1, ""cprCompetence"": 0.05, ""doorUnlockCompetence"": 0.03 }}
            ]
        }}");
        return path;
    }

    private static (EntityManager em, Entity rescuer, Entity victim) Build(
        string rescuerArchetype = "the-newbie",
        float  rescuerBias      = 0.85f,
        float  rescuerX         = 0f,
        float  victimX          = 1f,
        int    willpower        = 60,
        int    acuteStress      = 10,
        LS     victimState      = LS.Incapacitated,
        bool   victimChoking    = false)
    {
        var em = new EntityManager();

        var rescuer = em.CreateEntity();
        rescuer.Add(new NpcTag());
        rescuer.Add(new LifeStateComponent { State = LS.Alive });
        rescuer.Add(new PositionComponent { X = rescuerX, Z = 0f });
        rescuer.Add(new WillpowerComponent(willpower, willpower));
        rescuer.Add(new StressComponent { AcuteLevel = acuteStress });
        rescuer.Add(new NpcArchetypeComponent { ArchetypeId = rescuerArchetype });
        rescuer.Add(new ProximityComponent { ConversationRangeTiles = 2 });

        var victim = em.CreateEntity();
        victim.Add(new NpcTag());
        victim.Add(new LifeStateComponent { State = victimState });
        victim.Add(new PositionComponent { X = victimX, Z = 0f });
        if (victimChoking) victim.Add(new IsChokingTag());

        return (em, rescuer, victim);
    }

    // ── AT-02: High-bias rescuer sets Rescue intent ──────────────────────────

    [Fact]
    public void AT02_HighBiasRescuer_InRange_SetsRescueIntent()
    {
        var (em, rescuer, victim) = Build(rescuerArchetype: "the-newbie", rescuerBias: 0.85f);
        var sys = new RescueIntentSystem(MakeCatalog("the-newbie", 0.85f), DefaultCfg());
        sys.Update(em, 1f);

        Assert.True(rescuer.Has<IntendedActionComponent>());
        Assert.Equal(IntendedActionKind.Rescue, rescuer.Get<IntendedActionComponent>().Kind);
    }

    [Fact]
    public void AT02_RescueIntent_TargetEntityId_PointsToVictim()
    {
        var (em, rescuer, victim) = Build(rescuerArchetype: "the-newbie", rescuerBias: 0.85f);
        var sys = new RescueIntentSystem(MakeCatalog("the-newbie", 0.85f), DefaultCfg());
        sys.Update(em, 1f);

        var intent = rescuer.Get<IntendedActionComponent>();
        var victimIntId = BitConverter.ToInt32(victim.Id.ToByteArray(), 0);
        Assert.Equal(victimIntId, intent.TargetEntityId);
    }

    [Fact]
    public void AT02_RescueIntent_SetsMovementTarget()
    {
        var (em, rescuer, victim) = Build(rescuerArchetype: "the-newbie", rescuerBias: 0.85f);
        var sys = new RescueIntentSystem(MakeCatalog("the-newbie", 0.85f), DefaultCfg());
        sys.Update(em, 1f);

        Assert.True(rescuer.Has<MovementTargetComponent>());
        Assert.Equal(victim.Id, rescuer.Get<MovementTargetComponent>().TargetEntityId);
    }

    // ── AT-03: FoundersNephew (bias 0.10) + mid willpower → no intent ────────

    [Fact]
    public void AT03_FoundersNephew_LowBias_NoRescueIntent()
    {
        var (em, rescuer, victim) = Build(rescuerArchetype: "the-founders-nephew");
        var catalog = MakeCatalog("the-founders-nephew", 0.10f);
        var sys = new RescueIntentSystem(catalog, DefaultCfg());
        sys.Update(em, 1f);

        // Either no IntendedActionComponent, or not Rescue kind.
        if (rescuer.Has<IntendedActionComponent>())
            Assert.NotEqual(IntendedActionKind.Rescue, rescuer.Get<IntendedActionComponent>().Kind);
    }

    // ── Victim must be Incapacitated ─────────────────────────────────────────

    [Fact]
    public void VictimAlive_NoIntent()
    {
        var (em, rescuer, victim) = Build(victimState: LS.Alive);
        var sys = new RescueIntentSystem(MakeCatalog("the-newbie", 0.85f), DefaultCfg());
        sys.Update(em, 1f);

        if (rescuer.Has<IntendedActionComponent>())
            Assert.NotEqual(IntendedActionKind.Rescue, rescuer.Get<IntendedActionComponent>().Kind);
    }

    [Fact]
    public void VictimDeceased_NoIntent()
    {
        var (em, rescuer, victim) = Build(victimState: LS.Deceased);
        var sys = new RescueIntentSystem(MakeCatalog("the-newbie", 0.85f), DefaultCfg());
        sys.Update(em, 1f);

        if (rescuer.Has<IntendedActionComponent>())
            Assert.NotEqual(IntendedActionKind.Rescue, rescuer.Get<IntendedActionComponent>().Kind);
    }

    // ── Victim out of awareness range ────────────────────────────────────────

    [Fact]
    public void VictimOutOfAwarenessRange_NoIntent()
    {
        // Place victim 5 tiles away, beyond the 3.0 awareness range
        var (em, rescuer, victim) = Build(rescuerX: 0f, victimX: 5f);
        var sys = new RescueIntentSystem(MakeCatalog("the-newbie", 0.85f), DefaultCfg());
        sys.Update(em, 1f);

        if (rescuer.Has<IntendedActionComponent>())
            Assert.NotEqual(IntendedActionKind.Rescue, rescuer.Get<IntendedActionComponent>().Kind);
    }
}
