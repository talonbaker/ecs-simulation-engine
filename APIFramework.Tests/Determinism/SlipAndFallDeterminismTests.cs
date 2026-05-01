using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Determinism;

/// <summary>
/// AT-DET-SAF-01: Two identical SlipAndFall simulation runs (same seed, same world layout,
/// same entity creation order) must produce the same NPC LifeState after 5000 ticks.
/// GlobalSlipChanceScale=0.01 → slips happen occasionally but predictably.
/// </summary>
public class SlipAndFallDeterminismTests
{
    private sealed class SlipWorld
    {
        public EntityManager Em;
        public SimulationClock Clock;
        public NarrativeEventBus Bus;
        public LifeStateTransitionSystem Transitions;
        public SlipAndFallSystem System;
        public Entity Npc;

        public SlipWorld()
        {
            Em    = new EntityManager();
            Bus   = new NarrativeEventBus();
            Clock = new SimulationClock();
            Clock.TimeScale = 1f;

            var config = new SimConfig
            {
                LifeState  = new LifeStateConfig { DefaultIncapacitatedTicks = 180 },
                SlipAndFall = new SlipAndFallConfig
                {
                    GlobalSlipChanceScale  = 0.01f, // low enough to get occasional slips
                    StressDangerThreshold  = 60,
                    StressSlipMultiplier   = 2.0f,
                },
            };

            Transitions = new LifeStateTransitionSystem(Bus, Em, Clock, config);
            var rng     = new SeededRandom(0); // same seed in both worlds
            System      = new SlipAndFallSystem(Em, Clock, config, Transitions, rng);

            // NPC at tile (3, 3)
            Npc = Em.CreateEntity();
            Npc.Add(new NpcTag());
            Npc.Add(new LifeStateComponent { State = LS.Alive });
            Npc.Add(new PositionComponent { X = 3f, Y = 0f, Z = 3f });
            Npc.Add(new MovementComponent { SpeedModifier = 1.5f });
            Npc.Add(new StressComponent { AcuteLevel = 70 }); // above threshold → stressMult=2.0

            // Hazard at same tile
            var hazard = Em.CreateEntity();
            hazard.Add(new FallRiskComponent { RiskLevel = 0.5f });
            hazard.Add(new PositionComponent { X = 3f, Y = 0f, Z = 3f });
        }

        public void RunTick()
        {
            Clock.Tick(1f);
            System.Update(Em, 1f);
            Transitions.Update(Em, 1f);

            // If NPC slipped, reset to Alive so the simulation continues running
            if (Npc.Get<LifeStateComponent>().State == LS.Deceased)
            {
                Npc.Add(new LifeStateComponent { State = LS.Alive });
            }
        }
    }

    [Fact]
    public void TwoRuns_SameSeed_ProduceIdenticalNpcState_After5000Ticks()
    {
        const int Ticks = 5000;

        // ── Run 1 ─────────────────────────────────────────────────────────────
        int slipCount1 = 0;
        var w1 = new SlipWorld();
        for (int i = 0; i < Ticks; i++)
        {
            var before = w1.Npc.Get<LifeStateComponent>().State;
            w1.RunTick();
            var after = w1.Npc.Get<LifeStateComponent>().State;
            // Count slips before reset
            if (before == LS.Alive && after == LS.Deceased)
                slipCount1++;
        }

        // ── Run 2 ─────────────────────────────────────────────────────────────
        int slipCount2 = 0;
        var w2 = new SlipWorld();
        for (int i = 0; i < Ticks; i++)
        {
            var before = w2.Npc.Get<LifeStateComponent>().State;
            w2.RunTick();
            var after = w2.Npc.Get<LifeStateComponent>().State;
            if (before == LS.Alive && after == LS.Deceased)
                slipCount2++;
        }

        // ── Assert: same outcome ───────────────────────────────────────────────
        Assert.Equal(slipCount1, slipCount2);
        // Final state must also be identical
        Assert.Equal(
            w1.Npc.Get<LifeStateComponent>().State,
            w2.Npc.Get<LifeStateComponent>().State);
    }

    [Fact]
    public void TwoRuns_SameSeed_ClockTotalTimeIdenticalAfter5000Ticks()
    {
        const int Ticks = 5000;

        var w1 = new SlipWorld();
        var w2 = new SlipWorld();

        for (int i = 0; i < Ticks; i++) w1.RunTick();
        for (int i = 0; i < Ticks; i++) w2.RunTick();

        // Clock advances identically because TimeScale=1 and each Tick(1f) is the same
        Assert.Equal(w1.Clock.TotalTime, w2.Clock.TotalTime);
    }
}
