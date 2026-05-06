using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.LifeState;

/// <summary>
/// AT-SAF-03: NPC with very low FallRiskComponent (0.001), SpeedModifier=1.0, no stress,
/// GlobalSlipChanceScale=0.001 → slipChance=0.000001. Over 1000 ticks, NPC must never slip.
/// </summary>
public class SlipAndFallSystemLowRiskTests
{
    [Fact]
    public void AT03_LowRisk_1000Ticks_NpcNeverSlips()
    {
        var em    = new EntityManager();
        var bus   = new NarrativeEventBus();
        var clock = new SimulationClock();
        clock.TimeScale = 1f;

        var config = new SimConfig
        {
            LifeState  = new LifeStateConfig { DefaultIncapacitatedTicks = 180 },
            SlipAndFall = new SlipAndFallConfig
            {
                GlobalSlipChanceScale  = 0.001f,
                StressDangerThreshold  = 60,
                StressSlipMultiplier   = 2.0f,
            },
        };

        var transitions = new LifeStateTransitionSystem(bus, em, clock, config);
        var rng         = new SeededRandom(0);
        var system      = new SlipAndFallSystem(em, clock, config, transitions, rng);

        // NPC at tile (3, 3) — SpeedModifier=1.0, no stress
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LS.Alive });
        npc.Add(new PositionComponent { X = 3f, Y = 0f, Z = 3f });
        npc.Add(new MovementComponent { SpeedModifier = 1.0f });
        // No StressComponent → stressMult stays 1.0

        // Hazard at same tile — slipChance = 0.001 * 1.0 * 1.0 * 0.001 = 0.000001
        var hazard = em.CreateEntity();
        hazard.Add(new FallRiskComponent { RiskLevel = 0.001f });
        hazard.Add(new PositionComponent { X = 3f, Y = 0f, Z = 3f });

        // Run 1000 ticks, advancing clock each time
        for (int tick = 0; tick < 1000; tick++)
        {
            clock.Tick(1f); // advance clock so TotalTime changes each tick
            system.Update(em, 1f);
            transitions.Update(em, 1f);
        }

        Assert.Equal(LS.Alive, npc.Get<LifeStateComponent>().State);
        Assert.False(npc.Has<CauseOfDeathComponent>());
    }
}
