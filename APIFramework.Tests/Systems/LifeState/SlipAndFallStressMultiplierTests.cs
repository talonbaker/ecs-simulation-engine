using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.LifeState;

/// <summary>
/// AT-SAF-05: Stress multiplier gates.
///
/// Stressed   (AcuteLevel=80):  slipChance = 0.85 * 1.0 * 2.0 * 0.6 = 1.02 → always slips.
/// Unstressed (AcuteLevel=0):   slipChance = 0.85 * 1.0 * 1.0 * 0.6 = 0.51 → does not always slip.
///
/// Assertions:
///  - Stressed NPC slips on the very first Update.
///  - Unstressed NPC survives at least some ticks (not every roll < 0.51).
/// </summary>
public class SlipAndFallStressMultiplierTests
{
    private static (
        EntityManager em,
        NarrativeEventBus bus,
        SimulationClock clock,
        LifeStateTransitionSystem transitions,
        SlipAndFallSystem system,
        Entity npc,
        Entity hazard)
    Build()
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
                GlobalSlipChanceScale  = 0.6f,
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
        npc.Add(new MovementComponent { SpeedModifier = 1.0f });
        npc.Add(new StressComponent { AcuteLevel = 0 });

        var hazard = em.CreateEntity();
        hazard.Add(new FallRiskComponent { RiskLevel = 0.85f });
        hazard.Add(new PositionComponent { X = 3f, Y = 0f, Z = 3f });

        return (em, bus, clock, transitions, system, npc, hazard);
    }

    /// <summary>
    /// Stressed NPC (AcuteLevel=80): slipChance=1.02 → guaranteed slip on tick 1.
    /// </summary>
    [Fact]
    public void AT05a_Stressed_GuaranteedSlipOnTick1()
    {
        var (em, bus, clock, transitions, system, npc, _) = Build();

        // Set stress above threshold
        npc.Add(new StressComponent { AcuteLevel = 80 });

        clock.Tick(1f);
        system.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.Equal(LS.Deceased, npc.Get<LifeStateComponent>().State);
        Assert.Equal(CauseOfDeath.SlippedAndFell, npc.Get<CauseOfDeathComponent>().Cause);
    }

    /// <summary>
    /// Unstressed NPC (AcuteLevel=0): slipChance=0.51.
    /// Over 200 ticks, at least some ticks survive → not every roll is below 0.51.
    /// </summary>
    [Fact]
    public void AT05b_Unstressed_DoesNotAlwaysSlip_SomeTicksSurvive()
    {
        var (em, bus, clock, transitions, system, npc, _) = Build();

        // AcuteLevel=0 → below StressDangerThreshold=60, stressMult stays 1.0
        npc.Add(new StressComponent { AcuteLevel = 0 });

        int survivedTicks = 0;

        for (int tick = 0; tick < 200; tick++)
        {
            clock.Tick(1f);
            system.Update(em, 1f);
            transitions.Update(em, 1f);

            var state = npc.Get<LifeStateComponent>().State;
            if (state == LS.Alive)
            {
                survivedTicks++;
            }
            else
            {
                // Slip occurred — reset NPC to Alive for continued counting
                npc.Add(new LifeStateComponent { State = LS.Alive });
            }
        }

        // With slipChance=0.51, roughly 49% of ticks should survive → at least a few in 200
        Assert.True(survivedTicks > 0,
            $"Expected at least some ticks to survive with slipChance=0.51, but survived 0 out of 200.");
    }
}
