using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.LifeState;

/// <summary>
/// AT-SAF-04: No FallRiskComponent entity at the NPC's tile.
/// Even with GlobalSlipChanceScale=1.5 and a greedy config, no hazard means no slip.
/// The system iterates hazards at the NPC's tile and finds none → no transition queued.
/// </summary>
public class SlipAndFallSystemNoStainTests
{
    [Fact]
    public void AT04_NoHazardAtNpcTile_100Updates_NpcRemainsAlive()
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
                GlobalSlipChanceScale  = 1.5f,
                StressDangerThreshold  = 60,
                StressSlipMultiplier   = 2.0f,
            },
        };

        var transitions = new LifeStateTransitionSystem(bus, em, clock, config);
        var rng         = new SeededRandom(0);
        var system      = new SlipAndFallSystem(em, clock, config, transitions, rng);

        // NPC at tile (3, 3)
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LS.Alive });
        npc.Add(new PositionComponent { X = 3f, Y = 0f, Z = 3f });
        npc.Add(new MovementComponent { SpeedModifier = 2.0f });
        npc.Add(new StressComponent { AcuteLevel = 80 });

        // NO hazard entity at tile (3, 3) — hazard is completely absent

        for (int i = 0; i < 100; i++)
        {
            clock.Tick(1f);
            system.Update(em, 1f);
            transitions.Update(em, 1f);
        }

        Assert.Equal(LS.Alive, npc.Get<LifeStateComponent>().State);
        Assert.False(npc.Has<CauseOfDeathComponent>());
    }

    [Fact]
    public void AT04b_HazardExistsButAtDifferentTile_NpcRemainsAlive()
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
                GlobalSlipChanceScale  = 1.5f,
                StressDangerThreshold  = 60,
                StressSlipMultiplier   = 2.0f,
            },
        };

        var transitions = new LifeStateTransitionSystem(bus, em, clock, config);
        var rng         = new SeededRandom(0);
        var system      = new SlipAndFallSystem(em, clock, config, transitions, rng);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LS.Alive });
        npc.Add(new PositionComponent { X = 3f, Y = 0f, Z = 3f });

        // Hazard exists but at a different tile
        var hazard = em.CreateEntity();
        hazard.Add(new FallRiskComponent { RiskLevel = 0.99f });
        hazard.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f }); // not (3,3)

        for (int i = 0; i < 100; i++)
        {
            clock.Tick(1f);
            system.Update(em, 1f);
            transitions.Update(em, 1f);
        }

        Assert.Equal(LS.Alive, npc.Get<LifeStateComponent>().State);
    }
}
