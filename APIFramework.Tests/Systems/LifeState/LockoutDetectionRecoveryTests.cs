using System.Collections.Generic;
using System.Linq;
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
/// AT-LDS-04: Lockout recovery.
/// NPC is locked in for 3 days (budget 5→4→3). Then obstacles are removed and bus is notified.
/// On the 4th Update call, the path to the exit becomes reachable → LockedInComponent removed,
/// NPC remains Alive.
/// </summary>
public class LockoutDetectionRecoveryTests
{
    [Fact]
    public void AT04_ObstaclesRemoved_NpcRecoversFroLockout()
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

        // NPC at (1, 1) — very hungry
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LS.Alive });
        npc.Add(new PositionComponent { X = 1f, Y = 0f, Z = 1f });
        npc.Add(new MetabolismComponent { Satiation = 0f });

        // Obstacle wall at x=2, y=0..4 (blocks path from (1,1) to (4,4))
        var obstacles = new List<Entity>();
        for (int y = 0; y <= 4; y++)
        {
            var obs = em.CreateEntity();
            obs.Add(new ObstacleTag());
            obs.Add(new PositionComponent { X = 2f, Y = 0f, Z = y });
            obstacles.Add(obs);
        }

        // Exit anchor at (4, 4)
        var exitAnchor = em.CreateEntity();
        exitAnchor.Add(new NamedAnchorComponent { Tag = "outdoor", Description = "Exit" });
        exitAnchor.Add(new PositionComponent { X = 4f, Y = 0f, Z = 4f });

        // ── Call 1: First lockout detection (budget=5) ────────────────────────
        clock.Tick(64800f); // advance to 18:00
        system.Update(em, 1f);
        transitions.Update(em, 1f);
        Assert.True(npc.Has<LockedInComponent>());
        Assert.Equal(5, npc.Get<LockedInComponent>().StarvationTickBudget);

        // ── Call 2: Day 2 (budget=4) ──────────────────────────────────────────
        clock.Tick(86400f);
        system.Update(em, 1f);
        transitions.Update(em, 1f);
        Assert.True(npc.Has<LockedInComponent>());
        Assert.Equal(4, npc.Get<LockedInComponent>().StarvationTickBudget);

        // ── Call 3: Day 3 (budget=3) ──────────────────────────────────────────
        clock.Tick(86400f);
        system.Update(em, 1f);
        transitions.Update(em, 1f);
        Assert.True(npc.Has<LockedInComponent>());
        Assert.Equal(3, npc.Get<LockedInComponent>().StarvationTickBudget);

        // ── Remove obstacles and invalidate path cache ─────────────────────────
        foreach (var obs in obstacles)
        {
            obs.Remove<ObstacleTag>();
        }
        // Emit a structural change so the path cache is cleared (cache.Clear() is called by the subscriber)
        structBus.Emit(
            StructuralChangeKind.ObstacleDetached,
            System.Guid.Empty,
            2, 0,
            2, 0,
            System.Guid.Empty,
            (long)clock.TotalTime);

        // ── Call 4: Day 4 — path is now clear → NPC recovers ─────────────────
        clock.Tick(86400f);
        system.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.False(npc.Has<LockedInComponent>(),
            "LockedInComponent should be removed once the exit is reachable again.");
        Assert.Equal(LS.Alive, npc.Get<LifeStateComponent>().State);
    }
}
