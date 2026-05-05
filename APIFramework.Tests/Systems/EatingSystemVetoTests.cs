using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-02: NPC with hunger 120, bodyImageEating: 90, willpower 80 does not eat
///        over 1000 ticks.
/// AT-03: Same NPC with willpower 5 eats per the existing eating cadence.
/// AT-06: High stress relaxation: NPC with bodyImageEating: 90, willpower 80,
///        AcuteLevel: 90 eats (stress breaks the gate).
/// AT-07: NPC with no InhibitionsComponent is never vetoed.
/// </summary>
public class EatingSystemVetoTests
{
    private static readonly FeedingSystemConfig FeedCfg = new()
    {
        HungerThreshold     = 40f,
        NutritionQueueCap   = 240f,
        FoodFreshnessSeconds = 86_400f,
        FoodRotRate         = 0.001f,
        Banana              = new FoodItemConfig
        {
            VolumeMl       = 50f,
            EsophagusSpeed = 0.3f,
            Nutrients      = new NutrientProfile { Carbohydrates = 27f, Proteins = 1.3f,
                                                   Fats = 0.4f, Water = 89f }
        }
    };

    private static readonly PhysiologyGateConfig GateCfg = new()
    {
        VetoStrengthThreshold    = 0.50,
        LowWillpowerLeakageStart = 30,
        StressMaxRelaxation      = 0.7
    };

    /// <summary>
    /// Creates an entity whose hunger is always 120% effective (hunger=100, satiation=0)
    /// so Eat is always dominant with high urgency, well above the hunger threshold.
    /// </summary>
    private static (EntityManager em, Entity npc) MakeHungryNpc(
        int willpower,
        int inhibitionStrength,
        int acuteStress = 0,
        bool includeInhibitions = true)
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();

        // Satiation = 0 → Hunger = 100, drive score = 1.0 — always dominant
        npc.Add(new MetabolismComponent { Satiation = 0f, Hydration = 80f });
        npc.Add(new DriveComponent      { EatUrgency = 1.0f, DrinkUrgency = 0.1f });
        npc.Add(new StomachComponent    { CurrentVolumeMl = 0f, DigestionRate = 1f });
        npc.Add(new WillpowerComponent(willpower, willpower));
        npc.Add(new StressComponent     { AcuteLevel = acuteStress });

        if (includeInhibitions)
        {
            npc.Add(new InhibitionsComponent(new[]
            {
                new Inhibition(InhibitionClass.BodyImageEating, inhibitionStrength,
                               InhibitionAwareness.Hidden)
            }));
        }

        return (em, npc);
    }

    private static int CountEatEvents(EntityManager em, Entity npc, int ticks)
    {
        var gateSys  = new PhysiologyGateSystem(GateCfg);
        var feedSys  = new FeedingSystem(FeedCfg);
        int eatCount = 0;

        for (int i = 0; i < ticks; i++)
        {
            gateSys.Update(em, 1f);

            int before = em.Query<EsophagusTransitComponent>()
                           .Count(e => e.Get<EsophagusTransitComponent>().TargetEntityId == npc.Id);

            feedSys.Update(em, 1f);

            int after = em.Query<EsophagusTransitComponent>()
                          .Count(e => e.Get<EsophagusTransitComponent>().TargetEntityId == npc.Id);

            if (after > before) eatCount++;

            // Clear transit entities so the "throat busy" check doesn't permanently block
            foreach (var t in em.Query<EsophagusTransitComponent>().ToList())
                em.DestroyEntity(t);
        }

        return eatCount;
    }

    // -- AT-02 -----------------------------------------------------------------

    [Fact]
    public void AT02_HungerMax_BodyImageEating90_Willpower80_DoesNotEatOver1000Ticks()
    {
        // effectiveStrength = 0.9 × 1.0 × 1.0 = 0.9 ≥ 0.50 → veto holds
        var (em, npc) = MakeHungryNpc(willpower: 80, inhibitionStrength: 90);
        int eatCount  = CountEatEvents(em, npc, 1000);
        Assert.Equal(0, eatCount);
    }

    // -- AT-03 -----------------------------------------------------------------

    [Fact]
    public void AT03_HungerMax_BodyImageEating90_Willpower5_EatsAtLeastOnceIn1000Ticks()
    {
        // leakage = (30-5)/30 = 0.833; effectiveStrength = 0.9 × 0.167 = 0.15 < 0.50 → gate open
        var (em, npc) = MakeHungryNpc(willpower: 5, inhibitionStrength: 90);
        int eatCount  = CountEatEvents(em, npc, 1000);
        Assert.True(eatCount > 0, $"Expected at least one eat event but got {eatCount}");
    }

    // -- AT-06 -----------------------------------------------------------------

    [Fact]
    public void AT06_HighStress90_BodyImageEating90_Willpower80_EatsAtLeastOnceIn1000Ticks()
    {
        // stressMult = 1 - (90/100) × 0.7 = 0.37
        // effectiveStrength = 0.9 × 1.0 × 0.37 = 0.333 < 0.50 → gate open
        var (em, npc) = MakeHungryNpc(willpower: 80, inhibitionStrength: 90, acuteStress: 90);
        int eatCount  = CountEatEvents(em, npc, 1000);
        Assert.True(eatCount > 0, $"Expected at least one eat event but got {eatCount}");
    }

    // -- AT-07 -----------------------------------------------------------------

    [Fact]
    public void AT07_NoInhibitionsComponent_NeverVetoed_AlwaysEats()
    {
        // NPC without InhibitionsComponent should eat freely
        var (em, npc) = MakeHungryNpc(willpower: 80, inhibitionStrength: 90,
                                       includeInhibitions: false);
        int eatCount  = CountEatEvents(em, npc, 100);
        // Should eat on every tick where drive is dominant and throat is clear
        Assert.True(eatCount > 0, $"Expected eating without inhibitions but got {eatCount}");
    }

    [Fact]
    public void VetoDoesNotBlock_WhenInhibitionIs49Pct()
    {
        // effectiveStrength = 0.49 × 1.0 × 1.0 = 0.49 < 0.50 → not vetoed
        var (em, npc) = MakeHungryNpc(willpower: 80, inhibitionStrength: 49);
        int eatCount  = CountEatEvents(em, npc, 100);
        Assert.True(eatCount > 0, "Inhibition just under threshold should not veto eating");
    }
}
