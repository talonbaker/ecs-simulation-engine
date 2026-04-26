using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>AT-10: Neuroticism +2 NPC accumulates stress at least 1.4× faster than Neuroticism -2.</summary>
public class StressNeuroticismCouplingTests
{
    private static (EntityManager em, Entity npc, WillpowerEventQueue queue, StressSystem sys)
        BuildWithNeuroticism(int neuroticism, StressConfig cfg)
    {
        var clock = new SimulationClock();
        var queue = new WillpowerEventQueue();
        var bus   = new NarrativeEventBus();
        var sys   = new StressSystem(cfg, clock, queue, bus);

        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, neuroticism));
        npc.Add(new StressComponent { AcuteLevel = 0, LastDayUpdated = 1 });

        return (em, npc, queue, sys);
    }

    [Fact]
    public void HighNeuroticism_AccumulatesMoreStress_Than_LowNeuroticism()
    {
        // Spec: neuro +2 gains stress ≥ 1.4× faster than neuro -2
        // Formula: neuroFactor = 1.0 + N * neuroticismStressFactor
        // +2 → 1.0 + 2 * 0.2 = 1.4
        // -2 → 1.0 - 2 * 0.2 = 0.6
        // Actual ratio = 1.4 / 0.6 = 2.33 (well above 1.4)

        var cfg = new StressConfig
        {
            SuppressionStressGain = 1.5,
            NeuroticismStressFactor = 0.2,
            AcuteDecayPerTick = 0.0,   // no decay so levels accumulate cleanly
        };
        // 2 events/tick so low-neuroticism gain (2 × 1.5 × 0.6 = 1.8) truncates to 1/tick rather than 0
        const int suppressionEventsPerTick = 2;
        const int ticks = 10;

        var (emH, npcH, queueH, sysH) = BuildWithNeuroticism( 2, cfg);
        var (emL, npcL, queueL, sysL) = BuildWithNeuroticism(-2, cfg);

        for (int t = 0; t < ticks; t++)
        {
            int idH = WillpowerSystem.EntityIntId(npcH);
            int idL = WillpowerSystem.EntityIntId(npcL);

            for (int i = 0; i < suppressionEventsPerTick; i++)
            {
                queueH.Enqueue(new WillpowerEventSignal(idH, WillpowerEventKind.SuppressionTick, 1));
                queueL.Enqueue(new WillpowerEventSignal(idL, WillpowerEventKind.SuppressionTick, 1));
            }

            queueH.DrainAll();
            queueL.DrainAll();

            sysH.Update(emH, 1f);
            sysL.Update(emL, 1f);
        }

        int highAcute = npcH.Get<StressComponent>().AcuteLevel;
        int lowAcute  = npcL.Get<StressComponent>().AcuteLevel;

        Assert.True(lowAcute > 0, $"Low-neuroticism NPC should have some stress (got {lowAcute})");
        double ratio = (double)highAcute / lowAcute;
        Assert.True(ratio >= 1.4,
            $"High-neuroticism AcuteLevel ({highAcute}) should be ≥ 1.4× low ({lowAcute}); ratio={ratio:F2}");
    }

    [Fact]
    public void ZeroNeuroticism_UsesBaselineGain()
    {
        var cfg = new StressConfig
        {
            SuppressionStressGain   = 1.5,
            NeuroticismStressFactor = 0.2,
            AcuteDecayPerTick       = 0.0,
        };
        var (em, npc, queue, sys) = BuildWithNeuroticism(0, cfg);

        int entityId = WillpowerSystem.EntityIntId(npc);
        queue.Enqueue(new WillpowerEventSignal(entityId, WillpowerEventKind.SuppressionTick, 1));
        queue.DrainAll();

        sys.Update(em, 1f);

        // neuroFactor = 1.0 + 0 * 0.2 = 1.0; gain = 1.5 * 1.0 = 1.5 → AcuteLevel = 1
        Assert.Equal(1, npc.Get<StressComponent>().AcuteLevel);
    }
}
