using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// Unit tests for PhysiologyGateSystem — veto formula, inhibition mapping,
/// willpower leakage, and stress relaxation.
/// </summary>
public class PhysiologyGateSystemTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PhysiologyGateConfig DefaultCfg() => new()
    {
        VetoStrengthThreshold    = 0.50,
        LowWillpowerLeakageStart = 30,
        StressMaxRelaxation      = 0.7
    };

    private static EntityManager MakeEm() => new();

    /// <summary>Runs one tick of PhysiologyGateSystem and returns the entity.</summary>
    private static BlockedActionsComponent? RunGate(
        EntityManager em,
        Entity entity,
        PhysiologyGateConfig? cfg = null)
    {
        var sys = new PhysiologyGateSystem(cfg ?? DefaultCfg());
        sys.Update(em, 1f);
        return entity.Has<BlockedActionsComponent>()
            ? entity.Get<BlockedActionsComponent>()
            : null;
    }

    private static Entity MakeNpc(EntityManager em,
        InhibitionsComponent inhibitions,
        int willpower = 80,
        int acuteStress = 0)
    {
        var e = em.CreateEntity();
        e.Add(inhibitions);
        e.Add(new WillpowerComponent(willpower, willpower));
        e.Add(new StressComponent { AcuteLevel = acuteStress });
        return e;
    }

    // ── Leakage helpers (white-box) ───────────────────────────────────────────

    [Theory]
    [InlineData(80, 30, 0.0)]          // above threshold → no leakage
    [InlineData(30, 30, 0.0)]          // at threshold → no leakage
    [InlineData(15, 30, 0.5)]          // halfway → 50% leakage
    [InlineData(0,  30, 1.0)]          // at zero → full leakage
    public void LowWillpowerLeakage_Formula(int wp, int threshold, double expected)
    {
        var actual = PhysiologyGateSystem.LowWillpowerLeakage(wp, threshold);
        Assert.Equal(expected, actual, precision: 6);
    }

    [Theory]
    [InlineData(0,   0.7, 1.0)]        // no stress → full multiplier
    [InlineData(100, 0.7, 0.3)]        // max stress, max relaxation → 30% of veto
    [InlineData(50,  0.7, 0.65)]       // half stress
    public void StressLeakageMult_Formula(int acute, double maxRelaxation, double expected)
    {
        var actual = PhysiologyGateSystem.StressLeakageMult(acute, maxRelaxation);
        Assert.Equal(expected, actual, precision: 6);
    }

    // ── Core veto logic ───────────────────────────────────────────────────────

    [Fact]
    public void NpcWithNoInhibitions_NeverVetoed()
    {
        var em  = MakeEm();
        var npc = MakeNpc(em, new InhibitionsComponent(new List<Inhibition>()));
        var result = RunGate(em, npc);
        Assert.Null(result);
    }

    [Fact]
    public void NpcWithoutInhibitionsComponent_NeverVetoed()
    {
        var em = MakeEm();
        var e  = em.CreateEntity();
        e.Add(new WillpowerComponent(80, 80));
        e.Add(new StressComponent { AcuteLevel = 0 });
        // No InhibitionsComponent → gate system skips entirely
        var sys = new PhysiologyGateSystem(DefaultCfg());
        sys.Update(em, 1f);
        Assert.False(e.Has<BlockedActionsComponent>());
    }

    [Fact]
    public void BodyImageEating90_Willpower80_Stress0_VetoesEat()
    {
        // effectiveStrength = 0.9 × (1 - 0) × 1.0 = 0.9 ≥ 0.50 → blocked
        var em  = MakeEm();
        var npc = MakeNpc(em,
            new InhibitionsComponent(new[]
            {
                new Inhibition(InhibitionClass.BodyImageEating, 90, InhibitionAwareness.Hidden)
            }),
            willpower: 80, acuteStress: 0);

        var result = RunGate(em, npc);
        Assert.NotNull(result);
        Assert.True(result!.Value.Contains(BlockedActionClass.Eat));
    }

    [Fact]
    public void BodyImageEating90_Willpower5_Stress0_DoesNotVetoEat()
    {
        // willpower leakage = (30 - 5) / 30 = 0.833
        // effectiveStrength = 0.9 × (1 - 0.833) × 1.0 = 0.9 × 0.167 = 0.15 < 0.50 → not blocked
        var em  = MakeEm();
        var npc = MakeNpc(em,
            new InhibitionsComponent(new[]
            {
                new Inhibition(InhibitionClass.BodyImageEating, 90, InhibitionAwareness.Hidden)
            }),
            willpower: 5, acuteStress: 0);

        var result = RunGate(em, npc);
        // Either null or Eat not in blocked set
        Assert.True(result == null || !result.Value.Contains(BlockedActionClass.Eat));
    }

    [Fact]
    public void BodyImageEating90_Willpower80_AcuteStress90_DoesNotVetoEat()
    {
        // stressMult = 1 - (90/100) × 0.7 = 1 - 0.63 = 0.37
        // leakage = 0 (willpower 80 ≥ 30)
        // effectiveStrength = 0.9 × 1.0 × 0.37 = 0.333 < 0.50 → not blocked
        var em  = MakeEm();
        var npc = MakeNpc(em,
            new InhibitionsComponent(new[]
            {
                new Inhibition(InhibitionClass.BodyImageEating, 90, InhibitionAwareness.Hidden)
            }),
            willpower: 80, acuteStress: 90);

        var result = RunGate(em, npc);
        Assert.True(result == null || !result.Value.Contains(BlockedActionClass.Eat));
    }

    [Fact]
    public void Vulnerability80_Willpower80_Stress0_VetoesSleep()
    {
        // effectiveStrength = 0.8 × 1.0 × 1.0 = 0.8 ≥ 0.50 → blocked
        var em  = MakeEm();
        var npc = MakeNpc(em,
            new InhibitionsComponent(new[]
            {
                new Inhibition(InhibitionClass.Vulnerability, 80, InhibitionAwareness.Known)
            }),
            willpower: 80, acuteStress: 0);

        var result = RunGate(em, npc);
        Assert.NotNull(result);
        Assert.True(result!.Value.Contains(BlockedActionClass.Sleep));
    }

    [Fact]
    public void PublicEmotion90_Willpower70_Stress0_VetoesUrinateAndDefecate()
    {
        // effectiveStrength = 0.9 × 1.0 × 1.0 = 0.9 ≥ 0.50
        var em  = MakeEm();
        var npc = MakeNpc(em,
            new InhibitionsComponent(new[]
            {
                new Inhibition(InhibitionClass.PublicEmotion, 90, InhibitionAwareness.Known)
            }),
            willpower: 70, acuteStress: 0);

        var result = RunGate(em, npc);
        Assert.NotNull(result);
        Assert.True(result!.Value.Contains(BlockedActionClass.Urinate));
        Assert.True(result!.Value.Contains(BlockedActionClass.Defecate));
    }

    [Fact]
    public void WeakInhibition30_Willpower80_Stress0_DoesNotVeto()
    {
        // effectiveStrength = 0.30 × 1.0 × 1.0 = 0.30 < 0.50 → not blocked
        var em  = MakeEm();
        var npc = MakeNpc(em,
            new InhibitionsComponent(new[]
            {
                new Inhibition(InhibitionClass.BodyImageEating, 30, InhibitionAwareness.Known)
            }),
            willpower: 80, acuteStress: 0);

        var result = RunGate(em, npc);
        Assert.True(result == null || !result.Value.Contains(BlockedActionClass.Eat));
    }

    [Fact]
    public void MultipleInhibitions_EachMappedCorrectly()
    {
        var em  = MakeEm();
        var npc = MakeNpc(em,
            new InhibitionsComponent(new[]
            {
                new Inhibition(InhibitionClass.BodyImageEating, 80, InhibitionAwareness.Hidden),
                new Inhibition(InhibitionClass.Vulnerability,   80, InhibitionAwareness.Hidden),
                new Inhibition(InhibitionClass.PublicEmotion,   80, InhibitionAwareness.Known),
            }),
            willpower: 80, acuteStress: 0);

        var result = RunGate(em, npc);
        Assert.NotNull(result);
        Assert.True(result!.Value.Contains(BlockedActionClass.Eat));
        Assert.True(result!.Value.Contains(BlockedActionClass.Sleep));
        Assert.True(result!.Value.Contains(BlockedActionClass.Urinate));
        Assert.True(result!.Value.Contains(BlockedActionClass.Defecate));
    }

    [Fact]
    public void BlockedComponentRemovedWhenVetoLifts()
    {
        // First tick: strong inhibition → blocked
        var em  = MakeEm();
        var wp  = new WillpowerComponent(80, 80);
        var npc = em.CreateEntity();
        npc.Add(new InhibitionsComponent(new[]
        {
            new Inhibition(InhibitionClass.BodyImageEating, 90, InhibitionAwareness.Hidden)
        }));
        npc.Add(wp);
        npc.Add(new StressComponent { AcuteLevel = 0 });

        var sys = new PhysiologyGateSystem(DefaultCfg());
        sys.Update(em, 1f);
        Assert.True(npc.Has<BlockedActionsComponent>());

        // Weaken willpower to 0 → leakage = 1 → effective strength = 0 → veto lifts
        npc.Add(new WillpowerComponent(0, 80));
        sys.Update(em, 1f);
        Assert.False(npc.Has<BlockedActionsComponent>());
    }

    [Fact]
    public void HighestStrengthInhibitionWins_WhenDuplicateClasses()
    {
        // Two BodyImageEating inhibitions: 40 and 90 — effective should use 90
        var em  = MakeEm();
        var npc = MakeNpc(em,
            new InhibitionsComponent(new[]
            {
                new Inhibition(InhibitionClass.BodyImageEating, 40, InhibitionAwareness.Known),
                new Inhibition(InhibitionClass.BodyImageEating, 90, InhibitionAwareness.Hidden),
            }),
            willpower: 80, acuteStress: 0);

        var result = RunGate(em, npc);
        Assert.NotNull(result);
        Assert.True(result!.Value.Contains(BlockedActionClass.Eat));
    }
}
