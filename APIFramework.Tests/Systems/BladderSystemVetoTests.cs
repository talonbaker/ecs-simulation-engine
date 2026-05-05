using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-05: NPC with bladder critical, publicEmotion: 90, willpower 70
///        does not urinate autonomously over 500 ticks.
/// </summary>
public class BladderSystemVetoTests
{
    private static readonly PhysiologyGateConfig GateCfg = new()
    {
        VetoStrengthThreshold    = 0.50,
        LowWillpowerLeakageStart = 30,
        StressMaxRelaxation      = 0.7
    };

    /// <summary>
    /// Creates an entity with a full bladder (urination is dominant).
    /// Pee urgency = 1.0, bladder critical.
    /// </summary>
    private static (EntityManager em, Entity npc) MakeFullBladderNpc(
        int willpower,
        int publicEmotionStrength,
        int acuteStress = 0,
        bool includeInhibitions = true)
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();

        // Drive: pee urgency dominates
        npc.Add(new DriveComponent { PeeUrgency = 1.0f, EatUrgency = 0.1f });

        // Bladder: at capacity → critical
        npc.Add(new BladderComponent
        {
            VolumeML        = 100f,  // CapacityMl default
            CapacityMl      = 100f,
            UrgeThresholdMl = 70f,
            FillRate        = 0.01f
        });
        npc.Add(new BladderCriticalTag());
        npc.Add(new UrinationUrgeTag());

        npc.Add(new WillpowerComponent(willpower, willpower));
        npc.Add(new StressComponent { AcuteLevel = acuteStress });

        if (includeInhibitions)
        {
            npc.Add(new InhibitionsComponent(new[]
            {
                new Inhibition(InhibitionClass.PublicEmotion, publicEmotionStrength,
                               InhibitionAwareness.Known)
            }));
        }

        return (em, npc);
    }

    // -- AT-05 -----------------------------------------------------------------

    [Fact]
    public void AT05_BladderCritical_PublicEmotion90_Willpower70_DoesNotUrinateOver500Ticks()
    {
        // effectiveStrength = 0.9 × 1.0 × 1.0 = 0.9 ≥ 0.50 → urination vetoed
        var (em, npc) = MakeFullBladderNpc(willpower: 70, publicEmotionStrength: 90);

        var gateSys     = new PhysiologyGateSystem(GateCfg);
        var urinateSys  = new UrinationSystem();

        for (int i = 0; i < 500; i++)
        {
            gateSys.Update(em, 1f);
            urinateSys.Update(em, 1f);

            var bladder = npc.Get<BladderComponent>();
            Assert.True(bladder.VolumeML > 0f,
                $"NPC should not have urinated but did on tick {i + 1}");
        }
    }

    [Fact]
    public void LowWillpower_VetoBreaks_NpcUrginates()
    {
        // willpower = 5 → leakage = 0.833
        // effectiveStrength = 0.9 × 0.167 = 0.15 < 0.50 → not vetoed
        var (em, npc) = MakeFullBladderNpc(willpower: 5, publicEmotionStrength: 90);

        var gateSys    = new PhysiologyGateSystem(GateCfg);
        var urinateSys = new UrinationSystem();

        gateSys.Update(em, 1f);
        urinateSys.Update(em, 1f);

        var bladder = npc.Get<BladderComponent>();
        Assert.True(bladder.VolumeML == 0f,
            "NPC with low willpower should have urinated (bladder emptied)");
    }

    [Fact]
    public void NoInhibitionsComponent_AlwaysUrinates_WhenDominantIsPee()
    {
        var (em, npc) = MakeFullBladderNpc(willpower: 70, publicEmotionStrength: 90,
                                            includeInhibitions: false);

        var gateSys    = new PhysiologyGateSystem(GateCfg);
        var urinateSys = new UrinationSystem();

        gateSys.Update(em, 1f);
        urinateSys.Update(em, 1f);

        var bladder = npc.Get<BladderComponent>();
        Assert.True(bladder.VolumeML == 0f,
            "NPC without inhibitions should urinate freely when Pee is dominant");
    }

    [Fact]
    public void DefecationVeto_AlsoApplies_PublicEmotionBlocks()
    {
        // PublicEmotion also maps to Defecate class
        var em  = new EntityManager();
        var npc = em.CreateEntity();

        npc.Add(new DriveComponent { DefecateUrgency = 1.0f, EatUrgency = 0.1f });
        npc.Add(new ColonComponent
        {
            StoolVolumeMl   = 200f,
            CapacityMl      = 200f,
            UrgeThresholdMl = 100f
        });
        npc.Add(new BowelCriticalTag());
        npc.Add(new WillpowerComponent(70, 70));
        npc.Add(new StressComponent { AcuteLevel = 0 });
        npc.Add(new InhibitionsComponent(new[]
        {
            new Inhibition(InhibitionClass.PublicEmotion, 90, InhibitionAwareness.Known)
        }));

        var gateSys    = new PhysiologyGateSystem(GateCfg);
        var defecateSys = new DefecationSystem();

        for (int i = 0; i < 100; i++)
        {
            gateSys.Update(em, 1f);
            defecateSys.Update(em, 1f);

            var colon = npc.Get<ColonComponent>();
            Assert.True(colon.StoolVolumeMl > 0f,
                $"Defecation should be vetoed by publicEmotion but fired on tick {i + 1}");
        }
    }
}
