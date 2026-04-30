using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.LifeState;

/// <summary>
/// AT-LDS-02: NPC at (1,1), hungry (Satiation=0), exit at (4,4) in a 5x5 world.
/// Obstacles at x=2 for y in [0..4] wall off the right side completely.
/// After LockoutDetectionSystem.Update at lockout hour, NPC must have LockedInComponent
/// with StarvationTickBudget=5.
/// </summary>
public class LockoutDetectionPathUnreachableTests
{
    public static (
        EntityManager em,
        SimulationClock clock,
        LifeStateTransitionSystem transitions,
        LockoutDetectionSystem system,
        Entity npc,
        NarrativeEventBus bus)
    BuildUnreachableWorld()
    {
        var em    = new EntityManager();
        var bus   = new NarrativeEventBus();
        var clock = new SimulationClock();
        clock.TimeScale = 1f;

        var config = new SimConfig
        {
            LifeState  = new LifeStateConfig { DefaultIncapacitatedTicks = 180 },
            Lockout = new LockoutConfig
            {
                LockoutCheckHour      = 18.0f,
                LockoutHungerThreshold = 95,
                StarvationTicks       = 5,
                ExitNamedAnchorTag    = "outdoor",
            },
        };

        var transitions = new LifeStateTransitionSystem(bus, em, clock, config);
        var rng         = new SeededRandom(0);
        var pathCache   = new PathfindingCache(512);
        var structBus   = new StructuralChangeBus();
        structBus.Subscribe(_ => pathCache.Clear());
        var pathSvc     = new PathfindingService(em, 5, 5, new MovementConfig(), pathCache, structBus);
        var system      = new LockoutDetectionSystem(em, clock, config, pathSvc, transitions, rng);

        // NPC at (1, 1) — very hungry (Satiation=0)
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LS.Alive });
        npc.Add(new PositionComponent { X = 1f, Y = 0f, Z = 1f });
        npc.Add(new MetabolismComponent { Satiation = 0f });

        // Obstacle wall at x=2, y=0..4 — completely blocks path from (1,1) to (4,4)
        for (int y = 0; y <= 4; y++)
        {
            var obs = em.CreateEntity();
            obs.Add(new ObstacleTag());
            obs.Add(new PositionComponent { X = 2f, Y = 0f, Z = y });
        }

        // Exit anchor at (4, 4) — unreachable due to wall
        var exitAnchor = em.CreateEntity();
        exitAnchor.Add(new NamedAnchorComponent { Tag = "outdoor", Description = "Exit" });
        exitAnchor.Add(new PositionComponent { X = 4f, Y = 0f, Z = 4f });

        return (em, clock, transitions, system, npc, bus);
    }

    private static void AdvanceToLockoutHour(SimulationClock clock)
    {
        clock.Tick(64800f); // TotalTime = 64800 → hour 18.0
    }

    [Fact]
    public void AT02_PathUnreachable_HungryNpc_GetsLockedInComponent()
    {
        var (em, clock, transitions, system, npc, bus) = BuildUnreachableWorld();

        AdvanceToLockoutHour(clock);

        system.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.True(npc.Has<LockedInComponent>(),
            "NPC should be locked in when no exit is reachable and they are hungry.");

        var locked = npc.Get<LockedInComponent>();
        Assert.Equal(5, locked.StarvationTickBudget);
    }

    [Fact]
    public void AT02b_PathUnreachable_FirstDetectedTickIsCurrentTime()
    {
        var (em, clock, transitions, system, npc, bus) = BuildUnreachableWorld();

        AdvanceToLockoutHour(clock);
        double expectedTotalTime = clock.TotalTime;

        system.Update(em, 1f);

        var locked = npc.Get<LockedInComponent>();
        Assert.Equal((long)expectedTotalTime, locked.FirstDetectedTick);
    }
}
