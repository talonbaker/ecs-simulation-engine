using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-04: NPC with energy 5 (exhausted), vulnerability: 80, willpower 80
///        does not fall asleep over 1000 ticks.
/// </summary>
public class SleepSystemVetoTests
{
    private static readonly SleepSystemConfig SleepCfg = new() { WakeThreshold = 20f };

    private static readonly PhysiologyGateConfig GateCfg = new()
    {
        VetoStrengthThreshold    = 0.50,
        LowWillpowerLeakageStart = 30,
        StressMaxRelaxation      = 0.7
    };

    /// <summary>
    /// Creates an entity that the BrainSystem would score Sleep as dominant:
    /// DriveComponent.SleepUrgency = 1.0 so Sleep wins.
    /// Energy = 5 (exhausted), Sleepiness = 95.
    /// </summary>
    private static (EntityManager em, Entity npc) MakeExhaustedNpc(
        int willpower,
        int vulnerabilityStrength,
        int acuteStress = 0,
        bool includeInhibitions = true)
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();

        // Drive: sleep urgency dominates
        npc.Add(new DriveComponent { SleepUrgency = 1.0f, EatUrgency = 0.1f });

        // Energy state: very low energy, very high sleepiness
        npc.Add(new EnergyComponent { Energy = 5f, Sleepiness = 95f, IsSleeping = false });

        npc.Add(new WillpowerComponent(willpower, willpower));
        npc.Add(new StressComponent { AcuteLevel = acuteStress });

        if (includeInhibitions)
        {
            npc.Add(new InhibitionsComponent(new[]
            {
                new Inhibition(InhibitionClass.Vulnerability, vulnerabilityStrength,
                               InhibitionAwareness.Hidden)
            }));
        }

        return (em, npc);
    }

    // ── AT-04 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AT04_Exhausted_Vulnerability80_Willpower80_DoesNotSleepOver1000Ticks()
    {
        // effectiveStrength = 0.80 × 1.0 × 1.0 = 0.80 ≥ 0.50 → sleep vetoed
        var (em, npc) = MakeExhaustedNpc(willpower: 80, vulnerabilityStrength: 80);

        var gateSys  = new PhysiologyGateSystem(GateCfg);
        var sleepSys = new SleepSystem(SleepCfg);

        for (int i = 0; i < 1000; i++)
        {
            gateSys.Update(em, 1f);
            sleepSys.Update(em, 1f);

            var energy = npc.Get<EnergyComponent>();
            Assert.False(energy.IsSleeping,
                $"NPC should not sleep but fell asleep on tick {i + 1}");
        }
    }

    [Fact]
    public void LowWillpower_VetoBreaks_NpcEventuallyFallsAsleep()
    {
        // willpower = 5 → leakage = (30-5)/30 = 0.833
        // effectiveStrength = 0.8 × 0.167 = 0.133 < 0.50 → not vetoed
        var (em, npc) = MakeExhaustedNpc(willpower: 5, vulnerabilityStrength: 80);

        var gateSys  = new PhysiologyGateSystem(GateCfg);
        var sleepSys = new SleepSystem(SleepCfg);

        bool slept = false;
        for (int i = 0; i < 100; i++)
        {
            gateSys.Update(em, 1f);
            sleepSys.Update(em, 1f);

            if (npc.Get<EnergyComponent>().IsSleeping)
            {
                slept = true;
                break;
            }
        }

        Assert.True(slept, "Expected NPC with low willpower to eventually fall asleep");
    }

    [Fact]
    public void NoInhibitionsComponent_AlwaysFallsAsleep_WhenDominantIsSleep()
    {
        // No InhibitionsComponent → no veto possible
        var (em, npc) = MakeExhaustedNpc(willpower: 80, vulnerabilityStrength: 80,
                                          includeInhibitions: false);

        var gateSys  = new PhysiologyGateSystem(GateCfg);
        var sleepSys = new SleepSystem(SleepCfg);

        gateSys.Update(em, 1f);
        sleepSys.Update(em, 1f);

        Assert.True(npc.Get<EnergyComponent>().IsSleeping,
            "NPC without inhibitions should fall asleep when Sleep is dominant");
    }

    [Fact]
    public void SleepVeto_DoesNotBlock_WakeUp_AlreadySleeping()
    {
        // Even when vetoed, a sleeping NPC should be able to wake up
        // (the veto only blocks the fall-asleep transition)
        var (em, npc) = MakeExhaustedNpc(willpower: 80, vulnerabilityStrength: 80);

        // Force NPC to already be sleeping
        var energy = npc.Get<EnergyComponent>();
        energy.IsSleeping = true;
        energy.Sleepiness = 5f; // below wake threshold
        npc.Add(energy);

        // Set brain to NOT want sleep (e.g., hunger is now dominant)
        npc.Add(new DriveComponent { EatUrgency = 0.9f, SleepUrgency = 0.1f });

        var gateSys  = new PhysiologyGateSystem(GateCfg);
        var sleepSys = new SleepSystem(SleepCfg);

        gateSys.Update(em, 1f);
        sleepSys.Update(em, 1f);

        // Should wake up regardless of veto
        Assert.False(npc.Get<EnergyComponent>().IsSleeping,
            "Veto should not prevent waking up");
    }
}
