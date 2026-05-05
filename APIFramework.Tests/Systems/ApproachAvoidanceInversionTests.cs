using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// Shape-defining tests for the approach-avoidance inversion mechanic (AT-04 / AT-05).
/// Three ladder scenarios verify Approach → Approach → Avoid outputs as vulnerability rises.
/// AT-05 verifies that very low willpower breaks the avoidance gate back to Approach.
/// </summary>
public class ApproachAvoidanceInversionTests
{
    // -- Scaffolding -----------------------------------------------------------

    private static ActionSelectionConfig DefaultCfg() => new()
    {
        DriveCandidateThreshold      = 60,
        IdleScoreFloor               = 0.20,
        InversionStakeThreshold      = 0.55,
        InversionInhibitionThreshold = 0.50,
        SuppressionGiveUpFactor      = 0.30,
        SuppressionEpsilon           = 0.10,
        SuppressionEventMagnitudeScale = 5,
        PersonalityTieBreakWeight    = 0.05,
        MaxCandidatesPerTick         = 32,
        AvoidStandoffDistance        = 4
    };

    private static GridSpatialIndex MakeSpatial() =>
        new(new SpatialConfig { CellSizeTiles = 4, WorldSize = new() { Width = 64, Height = 64 } });

    /// <summary>
    /// Runs one tick and returns the intent kind for an NPC with the given setup.
    /// Target is placed 1 tile away (in proximity range), giving 1 nearby NPC witness
    /// and thus observabilityFactor = 0.5 + 0.1 = 0.6, stake = 0.6 > inversionStakeThreshold.
    /// </summary>
    private static IntendedActionKind RunScenario(
        int  attractionCurrent,
        int  vulnerabilityStrength,
        int  willpower,
        int  seed = 42)
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        // Place target 1 tile away from NPC (within awareness range 8).
        var target = em.CreateEntity();
        target.Add(new NpcTag());
        target.Add(new PositionComponent { X = 6f, Y = 0f, Z = 5f });
        spatial.Register(target, 6, 5);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new WillpowerComponent(willpower, willpower));
        npc.Add(new SocialDrivesComponent
        {
            Attraction = new DriveValue { Current = attractionCurrent, Baseline = attractionCurrent }
        });
        npc.Add(new InhibitionsComponent(new[]
        {
            new Inhibition(InhibitionClass.Vulnerability, vulnerabilityStrength, InhibitionAwareness.Hidden)
        }));
        spatial.Register(npc, 5, 5);

        var sys = new ActionSelectionSystem(
            spatial, new EntityRoomMembership(), queue, new SeededRandom(seed),
            DefaultCfg(), new ScheduleConfig(), em);

        sys.Update(em, 1f);

        return npc.Has<IntendedActionComponent>()
            ? npc.Get<IntendedActionComponent>().Kind
            : IntendedActionKind.Idle;
    }

    // -- Ladder: low / mid / high vulnerability → Approach → Approach → Avoid -

    [Fact]
    public void Ladder_LowVulnerability_Approach()
    {
        // strength 20: inhibition 0.2 < inversionInhibitionThreshold 0.5 → no inversion
        var kind = RunScenario(attractionCurrent: 80, vulnerabilityStrength: 20, willpower: 60);
        Assert.Equal(IntendedActionKind.Approach, kind);
    }

    [Fact]
    public void Ladder_MidVulnerability_Approach()
    {
        // strength 60: inhibition 0.6 > 0.5 AND stake 0.6 > 0.55 → inversion check fires
        // but giveUpStrength = (1-0.6) * 0.8 * 0.3 = 0.096
        // inversionBarrier = (0.6 - 0.5) * 0.6 = 0.06
        // 0.096 > 0.06 → willpower leakage overcomes barrier → stays Approach
        var kind = RunScenario(attractionCurrent: 80, vulnerabilityStrength: 60, willpower: 60);
        Assert.Equal(IntendedActionKind.Approach, kind);
    }

    [Fact]
    public void Ladder_HighVulnerability_Avoid()
    {
        // strength 80: inhibition 0.8 > 0.5 AND stake 0.6 > 0.55 → inversion check fires
        // giveUpStrength = (1-0.6) * 0.8 * 0.3 = 0.096
        // inversionBarrier = (0.8 - 0.5) * 0.6 = 0.18
        // 0.096 < 0.18 → inversion holds → Avoid
        var kind = RunScenario(attractionCurrent: 80, vulnerabilityStrength: 80, willpower: 60);
        Assert.Equal(IntendedActionKind.Avoid, kind);
    }

    // -- AT-04: Explicit Avoid test --------------------------------------------

    [Fact]
    public void AT04_HighVulnerability_MidWillpower_Avoid()
    {
        var kind = RunScenario(attractionCurrent: 80, vulnerabilityStrength: 80, willpower: 60);
        Assert.Equal(IntendedActionKind.Avoid, kind);
    }

    // -- AT-05: Very low willpower breaks the gate back to Approach ------------

    [Fact]
    public void AT05_HighVulnerability_VeryLowWillpower_GateBreaks_Approach()
    {
        // strength 80: inhibition 0.8 > 0.5 AND stake 0.6 > 0.55 → inversion check fires
        // giveUpStrength = (1-0.05) * 0.9 * 0.3 = 0.2565  (Attraction=90 for stronger leakage)
        // inversionBarrier = (0.8 - 0.5) * 0.6 = 0.18
        // 0.2565 > 0.18 → willpower leakage overcomes barrier → stays Approach (gate breaks)
        var kind = RunScenario(attractionCurrent: 90, vulnerabilityStrength: 80, willpower: 5);
        Assert.Equal(IntendedActionKind.Approach, kind);
    }

    // -- Avoid produces MovementTargetComponent pointing away -----------------

    [Fact]
    public void Avoid_WritesMovementTargetAway_FromThreat()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        var target = em.CreateEntity();
        target.Add(new NpcTag());
        target.Add(new PositionComponent { X = 6f, Y = 0f, Z = 5f });
        spatial.Register(target, 6, 5);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new WillpowerComponent(60, 60));
        npc.Add(new SocialDrivesComponent
        {
            Attraction = new DriveValue { Current = 80, Baseline = 80 }
        });
        npc.Add(new InhibitionsComponent(new[]
        {
            new Inhibition(InhibitionClass.Vulnerability, 80, InhibitionAwareness.Hidden)
        }));
        spatial.Register(npc, 5, 5);

        var sys = new ActionSelectionSystem(
            spatial, new EntityRoomMembership(), queue, new SeededRandom(42),
            DefaultCfg(), new ScheduleConfig(), em);

        sys.Update(em, 1f);

        Assert.True(npc.Has<IntendedActionComponent>());
        var intent = npc.Get<IntendedActionComponent>();
        Assert.Equal(IntendedActionKind.Avoid, intent.Kind);

        // MovementTargetComponent should be set and point to a flee entity.
        Assert.True(npc.Has<MovementTargetComponent>());
        var mt = npc.Get<MovementTargetComponent>();
        Assert.NotEqual(Guid.Empty, mt.TargetEntityId);
        Assert.NotEqual(target.Id, mt.TargetEntityId); // should NOT point to the target itself

        // The flee entity should exist and be positioned away from the target.
        Entity? fleeEntity = null;
        foreach (var e in em.GetAllEntities())
            if (e.Id == mt.TargetEntityId) { fleeEntity = e; break; }

        Assert.NotNull(fleeEntity);
        Assert.True(fleeEntity!.Has<PositionComponent>());
        var fleePos = fleeEntity.Get<PositionComponent>();

        // NPC at (5,5), target at (6,5): flee direction is (-1, 0) → flee target at (5 - 4, 5) = (1, 5)
        Assert.True(fleePos.X < 5f, $"Flee target X ({fleePos.X}) should be < NPC X (5)");
    }
}
