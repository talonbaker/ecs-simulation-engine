using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Integration;

/// <summary>
/// AT-09: ActionSelectionSystem writes MovementTargetComponent; PathfindingTriggerSystem
/// detects the change and computes a PathComponent.
///
/// Approach: target at (6, 5), NPC at (5, 5) → path leads toward (6, 5).
/// Avoid: threat at (6, 5), flee entity placed at (1, 5) by ActionSelectionSystem
///        → path leads away (final waypoint X &lt; NPC X).
/// </summary>
public class ActionSelectionToMovementTests
{
    private static ActionSelectionConfig DefaultActionCfg() => new()
    {
        DriveCandidateThreshold        = 60,
        IdleScoreFloor                 = 0.20,
        InversionStakeThreshold        = 0.55,
        InversionInhibitionThreshold   = 0.50,
        SuppressionGiveUpFactor        = 0.30,
        SuppressionEpsilon             = 0.10,
        SuppressionEventMagnitudeScale = 5,
        PersonalityTieBreakWeight      = 0.05,
        MaxCandidatesPerTick           = 32,
        AvoidStandoffDistance          = 4
    };

    private static GridSpatialIndex MakeSpatial() =>
        new(new SpatialConfig { CellSizeTiles = 4, WorldSize = new() { Width = 64, Height = 64 } });

    private static PathfindingTriggerSystem MakePathfinder(EntityManager em)
    {
        var cache = new PathfindingCache(512);
        var bus = new StructuralChangeBus();
        return new PathfindingTriggerSystem(new PathfindingService(em, worldWidth: 64, worldHeight: 64, new MovementConfig(), cache, bus));
    }

    // -- AT-09a: Approach ---------------------------------------------------------

    [Fact]
    public void AT09_Approach_WritesMovementTarget_AndPathfindingTrigger_ProducesPath()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var wq      = new WillpowerEventQueue();

        // Target 1 tile east; within awareness range.
        var target = em.CreateEntity();
        target.Add(new NpcTag());
        target.Add(new PositionComponent { X = 6f, Y = 0f, Z = 5f });
        spatial.Register(target, 6, 5);

        // NPC: high Attraction, low Vulnerability → Approach (no inversion)
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
            new Inhibition(InhibitionClass.Vulnerability, 20, InhibitionAwareness.Known)
        }));
        spatial.Register(npc, 5, 5);

        var actionSys = new ActionSelectionSystem(
            spatial, new EntityRoomMembership(), wq,
            new SeededRandom(42), DefaultActionCfg(), new ScheduleConfig(), em);
        var pathSys = MakePathfinder(em);

        actionSys.Update(em, 1f);

        Assert.True(npc.Has<IntendedActionComponent>());
        Assert.Equal(IntendedActionKind.Approach, npc.Get<IntendedActionComponent>().Kind);

        // ActionSelectionSystem should have written a MovementTargetComponent.
        Assert.True(npc.Has<MovementTargetComponent>(),
            "Expected MovementTargetComponent after Approach intent.");
        Assert.Equal(target.Id, npc.Get<MovementTargetComponent>().TargetEntityId);

        pathSys.Update(em, 1f);

        // PathfindingTriggerSystem should have produced a path.
        Assert.True(npc.Has<PathComponent>(), "Expected PathComponent after PathfindingTriggerSystem update.");
        var path = npc.Get<PathComponent>();
        Assert.NotNull(path.Waypoints);
        Assert.NotEmpty(path.Waypoints);

        // Final waypoint should be at or near target tile (6, 5).
        var last = path.Waypoints[path.Waypoints.Count - 1];
        Assert.Equal(6, last.X);
        Assert.Equal(5, last.Y);
    }

    // -- AT-09b: Avoid ------------------------------------------------------------

    [Fact]
    public void AT09_Avoid_WritesFleeTarget_AndPathfindingTrigger_ProducesPathAway()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var wq      = new WillpowerEventQueue();

        // Threat 1 tile east.
        var threat = em.CreateEntity();
        threat.Add(new NpcTag());
        threat.Add(new PositionComponent { X = 6f, Y = 0f, Z = 5f });
        spatial.Register(threat, 6, 5);

        // NPC: high Attraction, strong Vulnerability → Avoid (inversion)
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

        var actionSys = new ActionSelectionSystem(
            spatial, new EntityRoomMembership(), wq,
            new SeededRandom(42), DefaultActionCfg(), new ScheduleConfig(), em);
        var pathSys = MakePathfinder(em);

        actionSys.Update(em, 1f);

        Assert.True(npc.Has<IntendedActionComponent>());
        Assert.Equal(IntendedActionKind.Avoid, npc.Get<IntendedActionComponent>().Kind);

        // ActionSelectionSystem places a flee entity offset from the threat; NPC targets it.
        Assert.True(npc.Has<MovementTargetComponent>(),
            "Expected MovementTargetComponent after Avoid intent.");

        var mt         = npc.Get<MovementTargetComponent>();
        var fleeEntity = FindByGuid(em, mt.TargetEntityId);
        Assert.NotNull(fleeEntity);
        Assert.NotEqual(threat.Id, mt.TargetEntityId);  // NOT pointing at the threat itself

        var fleePos = fleeEntity!.Get<PositionComponent>();
        // Flee direction is west (away from threat at X=6): flee target should be X < NPC X (5).
        Assert.True(fleePos.X < 5f,
            $"Flee entity X ({fleePos.X}) should be west of NPC X (5) — away from threat.");

        pathSys.Update(em, 1f);

        Assert.True(npc.Has<PathComponent>(), "Expected PathComponent after PathfindingTriggerSystem update.");
        var path = npc.Get<PathComponent>();
        Assert.NotNull(path.Waypoints);
        Assert.NotEmpty(path.Waypoints);

        // The path should move the NPC west (decreasing X), away from the threat.
        var last = path.Waypoints[path.Waypoints.Count - 1];
        Assert.True(last.X < 5,
            $"Expected final waypoint X ({last.X}) to be west of NPC start X (5).");
    }

    // -- Helpers ------------------------------------------------------------------

    private static Entity? FindByGuid(EntityManager em, Guid id)
    {
        foreach (var e in em.GetAllEntities())
            if (e.Id == id) return e;
        return null;
    }
}
