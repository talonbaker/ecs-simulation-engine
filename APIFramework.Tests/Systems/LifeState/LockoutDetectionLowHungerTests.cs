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
/// AT-LDS-05: NPC with Satiation=50 (not hungry enough).
/// LockoutHungerThreshold=95 → system skips when Satiation > (100-95)=5.
/// Even with unreachable exit, NPC must NOT get LockedInComponent.
/// </summary>
public class LockoutDetectionLowHungerTests
{
    [Fact]
    public void AT05_LowHunger_Satiation50_NoLockedInComponent()
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

        // NPC at (1, 1) — Satiation=50 → 50 > (100-95)=5 → system skips the NPC
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LS.Alive });
        npc.Add(new PositionComponent { X = 1f, Y = 0f, Z = 1f });
        npc.Add(new MetabolismComponent { Satiation = 50f });

        // Obstacle wall at x=2, y=0..4 (same as unreachable setup)
        for (int y = 0; y <= 4; y++)
        {
            var obs = em.CreateEntity();
            obs.Add(new ObstacleTag());
            obs.Add(new PositionComponent { X = 2f, Y = 0f, Z = y });
        }

        // Exit anchor at (4, 4) — unreachable
        var exitAnchor = em.CreateEntity();
        exitAnchor.Add(new NamedAnchorComponent { Tag = "outdoor", Description = "Exit" });
        exitAnchor.Add(new PositionComponent { X = 4f, Y = 0f, Z = 4f });

        // Advance to lockout hour
        clock.Tick(64800f); // TotalTime = 64800 → hour 18.0

        system.Update(em, 1f);
        transitions.Update(em, 1f);

        // NPC is not hungry enough → no lockout
        Assert.False(npc.Has<LockedInComponent>(),
            "NPC with Satiation=50 should not be locked in (hunger below threshold).");
        Assert.Equal(LS.Alive, npc.Get<LifeStateComponent>().State);
    }

    [Fact]
    public void AT05b_ExactHungerBoundary_Satiation5_GetsLockedIn()
    {
        // Boundary: Satiation=5 → 5 <= (100-95)=5 → system processes the NPC
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

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LS.Alive });
        npc.Add(new PositionComponent { X = 1f, Y = 0f, Z = 1f });
        npc.Add(new MetabolismComponent { Satiation = 5f }); // exactly at threshold

        for (int y = 0; y <= 4; y++)
        {
            var obs = em.CreateEntity();
            obs.Add(new ObstacleTag());
            obs.Add(new PositionComponent { X = 2f, Y = 0f, Z = y });
        }

        var exitAnchor = em.CreateEntity();
        exitAnchor.Add(new NamedAnchorComponent { Tag = "outdoor", Description = "Exit" });
        exitAnchor.Add(new PositionComponent { X = 4f, Y = 0f, Z = 4f });

        clock.Tick(64800f);
        system.Update(em, 1f);
        transitions.Update(em, 1f);

        // Satiation=5 is exactly at the boundary → should be locked in
        Assert.True(npc.Has<LockedInComponent>(),
            "NPC with Satiation=5 (at boundary) should be locked in.");
    }

    [Fact]
    public void AT05c_JustAboveBoundary_Satiation6_NoLockedIn()
    {
        // Satiation=6 → 6 > (100-95)=5 → system skips
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

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LS.Alive });
        npc.Add(new PositionComponent { X = 1f, Y = 0f, Z = 1f });
        npc.Add(new MetabolismComponent { Satiation = 6f }); // just above threshold

        for (int y = 0; y <= 4; y++)
        {
            var obs = em.CreateEntity();
            obs.Add(new ObstacleTag());
            obs.Add(new PositionComponent { X = 2f, Y = 0f, Z = y });
        }

        var exitAnchor = em.CreateEntity();
        exitAnchor.Add(new NamedAnchorComponent { Tag = "outdoor", Description = "Exit" });
        exitAnchor.Add(new PositionComponent { X = 4f, Y = 0f, Z = 4f });

        clock.Tick(64800f);
        system.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.False(npc.Has<LockedInComponent>(),
            "NPC with Satiation=6 (just above threshold) should NOT be locked in.");
    }
}
