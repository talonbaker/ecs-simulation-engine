using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-08: Two 5000-tick runs with identical seeded inputs produce byte-identical
/// BlockedActionsComponent state at every tick.
/// </summary>
public class PhysiologyVetoDeterminismTests
{
    private const int Ticks = 5000;

    private static readonly PhysiologyGateConfig GateCfg = new()
    {
        VetoStrengthThreshold    = 0.50,
        LowWillpowerLeakageStart = 30,
        StressMaxRelaxation      = 0.7
    };

    /// <summary>
    /// Builds a small world with several NPCs having different inhibition profiles
    /// and wilpower / stress states, runs PhysiologyGateSystem for the given number
    /// of ticks, and returns a snapshot per tick of each entity's BlockedActionsComponent.
    ///
    /// The willpower and stress values are static (the test does not wire full systems).
    /// This is intentional: determinism of PhysiologyGateSystem is purely deterministic
    /// given the same input state — it contains no random sampling.
    /// </summary>
    private static List<(bool blocked, bool eatBlocked, bool sleepBlocked, bool urinateBlocked, bool defecateBlocked)>[]
        RunAndCollect(int ticks)
    {
        var em = new EntityManager();

        // NPC A: body image eating, high willpower
        var npcA = em.CreateEntity();
        npcA.Add(new InhibitionsComponent(new[]
        {
            new Inhibition(InhibitionClass.BodyImageEating, 80, InhibitionAwareness.Hidden)
        }));
        npcA.Add(new WillpowerComponent(80, 80));
        npcA.Add(new StressComponent { AcuteLevel = 10 });

        // NPC B: vulnerability (sleep gate), medium willpower
        var npcB = em.CreateEntity();
        npcB.Add(new InhibitionsComponent(new[]
        {
            new Inhibition(InhibitionClass.Vulnerability, 75, InhibitionAwareness.Known)
        }));
        npcB.Add(new WillpowerComponent(50, 50));
        npcB.Add(new StressComponent { AcuteLevel = 40 });

        // NPC C: publicEmotion (bladder/colon gate), low willpower (gate breaks)
        var npcC = em.CreateEntity();
        npcC.Add(new InhibitionsComponent(new[]
        {
            new Inhibition(InhibitionClass.PublicEmotion, 90, InhibitionAwareness.Known)
        }));
        npcC.Add(new WillpowerComponent(10, 50));
        npcC.Add(new StressComponent { AcuteLevel = 0 });

        // NPC D: no inhibitions (never vetoed)
        var npcD = em.CreateEntity();
        npcD.Add(new InhibitionsComponent(new List<Inhibition>()));
        npcD.Add(new WillpowerComponent(60, 60));
        npcD.Add(new StressComponent { AcuteLevel = 0 });

        var entities = new[] { npcA, npcB, npcC, npcD };
        var sys      = new PhysiologyGateSystem(GateCfg);

        var snapshots = new List<(bool, bool, bool, bool, bool)>[ticks];

        for (int t = 0; t < ticks; t++)
        {
            sys.Update(em, 1f);

            snapshots[t] = entities.Select(e =>
            {
                bool has  = e.Has<BlockedActionsComponent>();
                var  comp = has ? e.Get<BlockedActionsComponent>() : default;
                return (
                    has,
                    has && comp.Contains(BlockedActionClass.Eat),
                    has && comp.Contains(BlockedActionClass.Sleep),
                    has && comp.Contains(BlockedActionClass.Urinate),
                    has && comp.Contains(BlockedActionClass.Defecate)
                );
            }).ToList();
        }

        return snapshots;
    }

    // ── AT-08 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AT08_TwoRunsProduceBitIdenticalState()
    {
        var run1 = RunAndCollect(Ticks);
        var run2 = RunAndCollect(Ticks);

        Assert.Equal(run1.Length, run2.Length);

        for (int t = 0; t < Ticks; t++)
        {
            var snap1 = run1[t];
            var snap2 = run2[t];

            Assert.Equal(snap1.Count, snap2.Count);

            for (int i = 0; i < snap1.Count; i++)
            {
                Assert.Equal(snap1[i], snap2[i]);
            }
        }
    }

    [Fact]
    public void GateState_IsStableGivenFixedInputs()
    {
        // When input state never changes, the output should be the same every tick.
        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new InhibitionsComponent(new[]
        {
            new Inhibition(InhibitionClass.BodyImageEating, 80, InhibitionAwareness.Hidden)
        }));
        npc.Add(new WillpowerComponent(80, 80));
        npc.Add(new StressComponent { AcuteLevel = 10 });

        var sys = new PhysiologyGateSystem(GateCfg);

        bool? firstResult = null;
        for (int i = 0; i < 100; i++)
        {
            sys.Update(em, 1f);
            bool thisResult = npc.Has<BlockedActionsComponent>() &&
                              npc.Get<BlockedActionsComponent>().Contains(BlockedActionClass.Eat);

            if (firstResult is null)
                firstResult = thisResult;
            else
                Assert.Equal(firstResult, thisResult);
        }
    }
}
