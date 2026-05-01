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
/// AT-LDS-03: Starvation budget decrements once per game-day.
/// After 6 game-day Update() calls in an unreachable lockout scenario:
///   Call 1 → LockedInComponent attached, budget=5
///   Call 2 → budget=4
///   Call 3 → budget=3
///   Call 4 → budget=2
///   Call 5 → budget=1
///   Call 6 → NPC is Deceased(StarvedAlone), no LockedInComponent
/// </summary>
public class LockoutDetectionStarvationProgressionTests
{
    private static (
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

        // NPC at (1, 1) — very hungry
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LS.Alive });
        npc.Add(new PositionComponent { X = 1f, Y = 0f, Z = 1f });
        npc.Add(new MetabolismComponent { Satiation = 0f });

        // Obstacle wall at x=2, y=0..4
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

        return (em, clock, transitions, system, npc, bus);
    }

    /// <summary>
    /// Advance clock to lockout hour on a new day.
    /// On the first call, advance 18 hours. On each subsequent call, advance a full day (86400s)
    /// so DayNumber increments and the lockout check passes again at 18:00.
    /// </summary>
    private static void AdvanceToNextDayLockoutHour(SimulationClock clock, bool firstCall)
    {
        if (firstCall)
            clock.Tick(64800f); // 18 hours from start → TotalTime=64800 (18:00 of day 2)
        else
            clock.Tick(86400f); // advance one full day
    }

    [Fact]
    public void AT03_StarvationProgression_6Days_BudgetDecrementsAndNpcDies()
    {
        var (em, clock, transitions, system, npc, bus) = BuildUnreachableWorld();

        // ── Call 1: first lockout detection ──────────────────────────────────
        AdvanceToNextDayLockoutHour(clock, firstCall: true);
        system.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.True(npc.Has<LockedInComponent>(), "Call 1: LockedInComponent should be attached.");
        Assert.Equal(5, npc.Get<LockedInComponent>().StarvationTickBudget);

        // ── Call 2 ────────────────────────────────────────────────────────────
        AdvanceToNextDayLockoutHour(clock, firstCall: false);
        system.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.True(npc.Has<LockedInComponent>(), "Call 2: NPC should still be locked in.");
        Assert.Equal(4, npc.Get<LockedInComponent>().StarvationTickBudget);

        // ── Call 3 ────────────────────────────────────────────────────────────
        AdvanceToNextDayLockoutHour(clock, firstCall: false);
        system.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.True(npc.Has<LockedInComponent>(), "Call 3: NPC should still be locked in.");
        Assert.Equal(3, npc.Get<LockedInComponent>().StarvationTickBudget);

        // ── Call 4 ────────────────────────────────────────────────────────────
        AdvanceToNextDayLockoutHour(clock, firstCall: false);
        system.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.True(npc.Has<LockedInComponent>(), "Call 4: NPC should still be locked in.");
        Assert.Equal(2, npc.Get<LockedInComponent>().StarvationTickBudget);

        // ── Call 5 ────────────────────────────────────────────────────────────
        AdvanceToNextDayLockoutHour(clock, firstCall: false);
        system.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.True(npc.Has<LockedInComponent>(), "Call 5: NPC should still be locked in.");
        Assert.Equal(1, npc.Get<LockedInComponent>().StarvationTickBudget);

        // ── Call 6: budget expires → death ───────────────────────────────────
        AdvanceToNextDayLockoutHour(clock, firstCall: false);
        system.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.Equal(LS.Deceased, npc.Get<LifeStateComponent>().State);
        Assert.False(npc.Has<LockedInComponent>(), "Call 6: LockedInComponent should be removed on death.");
        Assert.True(npc.Has<CauseOfDeathComponent>());
        Assert.Equal(CauseOfDeath.StarvedAlone, npc.Get<CauseOfDeathComponent>().Cause);
    }
}
