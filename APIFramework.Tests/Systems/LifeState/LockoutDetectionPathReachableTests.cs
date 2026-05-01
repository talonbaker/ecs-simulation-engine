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
/// AT-LDS-01: NPC at (1,1), hungry (Satiation=0), exit anchor reachable at (8,8)
/// in an obstacle-free 10x10 world. After LockoutDetectionSystem.Update at lockout hour,
/// NPC must NOT have LockedInComponent because a path to the exit exists.
/// </summary>
public class LockoutDetectionPathReachableTests
{
    private static void AdvanceToLockoutHour(SimulationClock clock)
    {
        // From TotalTime=0, advance by 18 hours worth of game-seconds
        // IsAtLockoutHour checks: TotalTime % 86400 / 3600 >= 18.0
        clock.TimeScale = 1f;
        clock.Tick(64800f); // TotalTime = 64800 = 18 * 3600 → hour 18.0
    }

    [Fact]
    public void AT01_PathReachable_HungryNpc_NoLockedInComponent()
    {
        var em    = new EntityManager();
        var bus   = new NarrativeEventBus();
        var clock = new SimulationClock();

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
        var bus2        = new StructuralChangeBus();
        bus2.Subscribe(_ => pathCache.Clear());
        var pathSvc     = new PathfindingService(em, 10, 10, new MovementConfig(), pathCache, bus2);
        var system      = new LockoutDetectionSystem(em, clock, config, pathSvc, transitions, rng);

        // NPC at (1, 1) — hungry (Satiation=0)
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LS.Alive });
        npc.Add(new PositionComponent { X = 1f, Y = 0f, Z = 1f });
        npc.Add(new MetabolismComponent { Satiation = 0f }); // 0 <= 100-95=5 → hungry

        // Exit anchor at (8, 8) — reachable, no obstacles
        var exitAnchor = em.CreateEntity();
        exitAnchor.Add(new NamedAnchorComponent { Tag = "outdoor", Description = "Exit" });
        exitAnchor.Add(new PositionComponent { X = 8f, Y = 0f, Z = 8f });

        AdvanceToLockoutHour(clock);

        system.Update(em, 1f);
        transitions.Update(em, 1f);

        // NPC can reach exit → no lockout
        Assert.False(npc.Has<LockedInComponent>(),
            "NPC should NOT be locked in when an exit is reachable.");
        Assert.Equal(LS.Alive, npc.Get<LifeStateComponent>().State);
    }
}
