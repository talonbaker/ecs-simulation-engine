using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Determinism;

/// <summary>
/// AT-DET-LDS-01: Two identical LockoutDetection scenarios (same seed, same world layout,
/// same entity creation order) must produce the same NPC state after 6 game-day Update calls.
/// Both runs should end with NPC in Deceased(StarvedAlone) state at the same clock tick.
/// </summary>
public class LockoutDeterminismTests
{
    private sealed class LockoutWorld
    {
        public EntityManager Em;
        public SimulationClock Clock;
        public NarrativeEventBus Bus;
        public LifeStateTransitionSystem Transitions;
        public LockoutDetectionSystem LockoutSystem;
        public Entity Npc;

        public LockoutWorld()
        {
            Em    = new EntityManager();
            Bus   = new NarrativeEventBus();
            Clock = new SimulationClock();
            Clock.TimeScale = 1f;

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

            Transitions = new LifeStateTransitionSystem(Bus, Em, Clock, config);
            var rng     = new SeededRandom(0);
            var cache   = new PathfindingCache(512);
            var structBus = new StructuralChangeBus();
            structBus.Subscribe(_ => cache.Clear());
            var pathSvc = new PathfindingService(Em, 5, 5, new MovementConfig(), cache, structBus);
            LockoutSystem = new LockoutDetectionSystem(Em, Clock, config, pathSvc, Transitions, rng);

            // NPC at (1, 1) — very hungry from day 1
            Npc = Em.CreateEntity();
            Npc.Add(new NpcTag());
            Npc.Add(new LifeStateComponent { State = LS.Alive });
            Npc.Add(new PositionComponent { X = 1f, Y = 0f, Z = 1f });
            Npc.Add(new MetabolismComponent { Satiation = 0f });

            // Obstacle wall at x=2, y=0..4
            for (int y = 0; y <= 4; y++)
            {
                var obs = Em.CreateEntity();
                obs.Add(new ObstacleTag());
                obs.Add(new PositionComponent { X = 2f, Y = 0f, Z = y });
            }

            // Exit anchor at (4, 4) — unreachable
            var exitAnchor = Em.CreateEntity();
            exitAnchor.Add(new NamedAnchorComponent { Tag = "outdoor", Description = "Exit" });
            exitAnchor.Add(new PositionComponent { X = 4f, Y = 0f, Z = 4f });
        }

        public void RunAllDays()
        {
            // Day 1: advance to lockout hour
            Clock.Tick(64800f);
            LockoutSystem.Update(Em, 1f);
            Transitions.Update(Em, 1f);

            // Days 2–6: advance one full day each call
            for (int day = 2; day <= 6; day++)
            {
                Clock.Tick(86400f);
                LockoutSystem.Update(Em, 1f);
                Transitions.Update(Em, 1f);
            }
        }
    }

    [Fact]
    public void TwoRuns_SameWorld_ProduceIdenticalDeathState()
    {
        var w1 = new LockoutWorld();
        var w2 = new LockoutWorld();

        w1.RunAllDays();
        w2.RunAllDays();

        // Both NPCs must be Deceased(StarvedAlone)
        Assert.Equal(LS.Deceased, w1.Npc.Get<LifeStateComponent>().State);
        Assert.Equal(LS.Deceased, w2.Npc.Get<LifeStateComponent>().State);

        Assert.Equal(
            w1.Npc.Get<CauseOfDeathComponent>().Cause,
            w2.Npc.Get<CauseOfDeathComponent>().Cause);

        Assert.Equal(CauseOfDeath.StarvedAlone, w1.Npc.Get<CauseOfDeathComponent>().Cause);
    }

    [Fact]
    public void TwoRuns_SameWorld_DeathTickIsIdentical()
    {
        var w1 = new LockoutWorld();
        var w2 = new LockoutWorld();

        w1.RunAllDays();
        w2.RunAllDays();

        // The death tick (TotalTime at transition) must be identical across both runs
        long deathTick1 = w1.Npc.Get<CauseOfDeathComponent>().DeathTick;
        long deathTick2 = w2.Npc.Get<CauseOfDeathComponent>().DeathTick;

        Assert.Equal(deathTick1, deathTick2);
    }

    [Fact]
    public void TwoRuns_SameWorld_ClockTotalTimeIsIdentical()
    {
        var w1 = new LockoutWorld();
        var w2 = new LockoutWorld();

        w1.RunAllDays();
        w2.RunAllDays();

        Assert.Equal(w1.Clock.TotalTime, w2.Clock.TotalTime);
    }
}
