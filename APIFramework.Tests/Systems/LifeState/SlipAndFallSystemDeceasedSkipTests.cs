using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.LifeState;

/// <summary>
/// AT-SAF-06: A Deceased NPC is skipped by SlipAndFallSystem (LifeStateGuard.IsAlive guard).
/// No additional CauseOfDeathComponent is attached and no RequestTransition is queued.
/// </summary>
public class SlipAndFallSystemDeceasedSkipTests
{
    [Fact]
    public void AT06_AlreadyDeceased_SystemSkipsNpc_NoCauseOfDeathOverwrite()
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
                GlobalSlipChanceScale  = 1.5f, // greedy scale — would always slip if alive
                StressDangerThreshold  = 60,
                StressSlipMultiplier   = 2.0f,
            },
        };

        var transitions = new LifeStateTransitionSystem(bus, em, clock, config);
        var rng         = new SeededRandom(0);
        var system      = new SlipAndFallSystem(em, clock, config, transitions, rng);

        // NPC already Deceased with an existing cause (Choked)
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LS.Deceased });
        npc.Add(new CauseOfDeathComponent { Cause = CauseOfDeath.Choked, DeathTick = 0 });
        npc.Add(new PositionComponent { X = 3f, Y = 0f, Z = 3f });
        npc.Add(new MovementComponent { SpeedModifier = 2.0f });
        npc.Add(new StressComponent { AcuteLevel = 80 });

        // Hazard at same tile — would guarantee slip if NPC were alive
        var hazard = em.CreateEntity();
        hazard.Add(new FallRiskComponent { RiskLevel = 0.85f });
        hazard.Add(new PositionComponent { X = 3f, Y = 0f, Z = 3f });

        var candidatesEmitted = new System.Collections.Generic.List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += candidatesEmitted.Add;

        clock.Tick(1f);
        system.Update(em, 1f);
        transitions.Update(em, 1f);

        bus.OnCandidateEmitted -= candidatesEmitted.Add;

        // State must remain Deceased (not "re-killed")
        Assert.Equal(LS.Deceased, npc.Get<LifeStateComponent>().State);

        // The original cause of death (Choked) must be preserved — not overwritten with SlippedAndFell
        Assert.Equal(CauseOfDeath.Choked, npc.Get<CauseOfDeathComponent>().Cause);

        // No SlippedAndFell narrative candidate should have been emitted
        Assert.DoesNotContain(candidatesEmitted, c => c.Kind == NarrativeEventKind.SlippedAndFell);
    }

    [Fact]
    public void AT06b_AlreadyDeceased_NoLockedInNorTransitionQueued()
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
        npc.Add(new LifeStateComponent { State = LS.Deceased });
        npc.Add(new PositionComponent { X = 1f, Y = 0f, Z = 1f });

        var hazard = em.CreateEntity();
        hazard.Add(new FallRiskComponent { RiskLevel = 1.0f });
        hazard.Add(new PositionComponent { X = 1f, Y = 0f, Z = 1f });

        // Running system multiple times must not cause any state change
        for (int i = 0; i < 5; i++)
        {
            clock.Tick(1f);
            system.Update(em, 1f);
            transitions.Update(em, 1f);
        }

        Assert.Equal(LS.Deceased, npc.Get<LifeStateComponent>().State);
    }
}
