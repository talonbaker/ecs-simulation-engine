using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.LifeState;

/// <summary>
/// AT-SAF-01: NPC on a high-risk hazard tile with high speed and stress → guaranteed slip on tick 1.
/// AT-SAF-02: Hazard on different tile → no slip occurs.
/// </summary>
public class SlipAndFallSystemHighRiskTests
{
    private static (
        EntityManager em,
        NarrativeEventBus bus,
        SimulationClock clock,
        SimConfig config,
        LifeStateTransitionSystem transitions,
        SlipAndFallSystem system,
        Entity npc)
    Build(bool hazardAtSameTile = true)
    {
        var em     = new EntityManager();
        var bus    = new NarrativeEventBus();
        var clock  = new SimulationClock();
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

        // Hazard entity
        var hazard = em.CreateEntity();
        hazard.Add(new FallRiskComponent { RiskLevel = 0.85f });
        // slipChance = 0.85 * 2.0 * 2.0 * 1.5 = 5.1 → always >= 1 → guaranteed
        if (hazardAtSameTile)
            hazard.Add(new PositionComponent { X = 3f, Y = 0f, Z = 3f });
        else
            hazard.Add(new PositionComponent { X = 9f, Y = 0f, Z = 9f }); // different tile

        return (em, bus, clock, config, transitions, system, npc);
    }

    /// <summary>
    /// AT-SAF-01: slip chance = 5.1 (guaranteed). After one Update + transitions.Update,
    /// NPC must be Deceased with CauseOfDeathComponent.Cause == SlippedAndFell.
    /// </summary>
    [Fact]
    public void AT01_HighRisk_GuaranteedSlip_NpcBecomesDeceased()
    {
        var (em, bus, clock, config, transitions, system, npc) = Build(hazardAtSameTile: true);

        system.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.Equal(LS.Deceased, npc.Get<LifeStateComponent>().State);
        Assert.True(npc.Has<CauseOfDeathComponent>());
        Assert.Equal(CauseOfDeath.SlippedAndFell, npc.Get<CauseOfDeathComponent>().Cause);
    }

    /// <summary>
    /// AT-SAF-02: Hazard on a different tile → roll never applies to NPC → NPC stays Alive.
    /// </summary>
    [Fact]
    public void AT02_HazardOnDifferentTile_NoSlip_NpcRemainsAlive()
    {
        var (em, bus, clock, config, transitions, system, npc) = Build(hazardAtSameTile: false);

        system.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.Equal(LS.Alive, npc.Get<LifeStateComponent>().State);
        Assert.False(npc.Has<CauseOfDeathComponent>());
    }

    /// <summary>
    /// AT-SAF-01b: Narrative bus emits a SlippedAndFell candidate when slip occurs.
    /// </summary>
    [Fact]
    public void AT01b_HighRisk_NarrativeBusEmitsSlippedAndFell()
    {
        var (em, bus, clock, config, transitions, system, npc) = Build(hazardAtSameTile: true);

        var candidates = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += candidates.Add;

        system.Update(em, 1f);
        transitions.Update(em, 1f);

        bus.OnCandidateEmitted -= candidates.Add;

        Assert.Contains(candidates, c => c.Kind == NarrativeEventKind.SlippedAndFell);
    }
}
