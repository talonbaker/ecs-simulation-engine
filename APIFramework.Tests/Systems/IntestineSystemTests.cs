using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// Unit tests for the three intestine systems that form the GI elimination pipeline:
///   SmallIntestineSystem → LargeIntestineSystem → ColonSystem
///
/// PIPELINE CONTRACT (v0.7.3)
/// --------------------------
/// 1. DigestionSystem deposits ResidueFraction × digested volume as chyme into
///    SmallIntestineComponent (tested in DigestionSystemTests; assumed correct here).
/// 2. SmallIntestineSystem drains chyme at AbsorptionRate, passing
///    ResidueToLargeFraction of each processed batch to LargeIntestineComponent.
/// 3. LargeIntestineSystem reabsorbs water into MetabolismComponent.Hydration and
///    passes StoolFraction of mobility-processed content to ColonComponent.
/// 4. ColonSystem applies/removes DefecationUrgeTag and BowelCriticalTag based on
///    ColonComponent.StoolVolumeMl vs UrgeThresholdMl / CapacityMl.
///
/// Each system is tested in isolation with explicit setup so the tests are
/// self-documenting specifications of the per-tick contracts.
/// </summary>
public class IntestineSystemTests
{
    // ===========================================================================
    // SmallIntestineSystem
    // ===========================================================================

    private static SmallIntestineSystem SiSys => new();

    // -- Volume drainage --------------------------------------------------------

    [Fact]
    public void SI_ChymeVolume_DrainedAt_AbsorptionRate_Times_DeltaTime()
    {
        // processed = 5 * 2 = 10ml; 100 - 10 = 90ml remaining
        var (em, _) = BuildSi(chymeVol: 100f, absorptionRate: 5f);
        SiSys.Update(em, deltaTime: 2f);
        Assert.Equal(90f, GetSi(em).ChymeVolumeMl, precision: 3);
    }

    [Fact]
    public void SI_ChymeVolume_ClampedToZero_WhenAbsorptionExceedsContent()
    {
        // AbsorptionRate*dt = 200ml but only 30ml present → clamp to 0
        var (em, _) = BuildSi(chymeVol: 30f, absorptionRate: 200f);
        SiSys.Update(em, deltaTime: 1f);
        Assert.Equal(0f, GetSi(em).ChymeVolumeMl);
    }

    [Fact]
    public void SI_EmptySI_IsSkipped_NoException()
    {
        var (em, _) = BuildSi(chymeVol: 0f, absorptionRate: 5f);
        var ex = Record.Exception(() => SiSys.Update(em, deltaTime: 1f));
        Assert.Null(ex);
    }

    [Fact]
    public void SI_EmptySI_DoesNotChangeChyme()
    {
        var chyme = new NutrientProfile { Fiber = 10f };
        var (em, _) = BuildSi(chymeVol: 0f, absorptionRate: 5f, chyme: chyme);
        SiSys.Update(em, deltaTime: 5f);
        Assert.Equal(10f, GetSi(em).Chyme.Fiber);
    }

    // -- Chyme nutrient tracking ------------------------------------------------

    [Fact]
    public void SI_ChymeNutrients_DrainedProportionally()
    {
        // volume=100ml, rate=10, dt=1 → processed=10, ratio=0.1
        // Fiber=100g * 0.1 = 10g removed → 90g remaining
        var chyme = new NutrientProfile { Fiber = 100f };
        var (em, _) = BuildSi(chymeVol: 100f, absorptionRate: 10f, chyme: chyme);
        SiSys.Update(em, deltaTime: 1f);
        Assert.Equal(90f, GetSi(em).Chyme.Fiber, precision: 2);
    }

    [Fact]
    public void SI_AllChymeFields_DrainedByTheSameRatio()
    {
        // ratio = 10/100 = 0.1 → every field shrinks by 10%
        var chyme = new NutrientProfile
        {
            Carbohydrates = 10f,
            Fiber         = 20f,
            Water         = 30f,
            Potassium     = 400f,
        };
        var (em, _) = BuildSi(chymeVol: 100f, absorptionRate: 10f, chyme: chyme);
        SiSys.Update(em, deltaTime: 1f);
        var q = GetSi(em).Chyme;
        Assert.Equal(9f,   q.Carbohydrates, precision: 2);
        Assert.Equal(18f,  q.Fiber,         precision: 2);
        Assert.Equal(27f,  q.Water,         precision: 2);
        Assert.Equal(360f, q.Potassium,     precision: 2);
    }

    // -- Residue handoff to LargeIntestine -------------------------------------

    [Fact]
    public void SI_ResidueHandoff_AddsToLI_ContentVolume()
    {
        // processed=10ml, ResidueToLarge=0.4 → LI receives 4ml
        var (em, _) = BuildSi(chymeVol: 100f, absorptionRate: 10f, residueFrac: 0.4f,
                               withLi: true, liContent: 0f);
        SiSys.Update(em, deltaTime: 1f);
        Assert.Equal(4f, GetLi(em).ContentVolumeMl, precision: 3);
    }

    [Fact]
    public void SI_ResidueHandoff_AddedTo_ExistingLIContent()
    {
        // LI starts at 50ml; receives 4ml more → 54ml
        var (em, _) = BuildSi(chymeVol: 100f, absorptionRate: 10f, residueFrac: 0.4f,
                               withLi: true, liContent: 50f);
        SiSys.Update(em, deltaTime: 1f);
        Assert.Equal(54f, GetLi(em).ContentVolumeMl, precision: 3);
    }

    [Fact]
    public void SI_ResidueHandoff_ClampedAt_LIMaxVolume()
    {
        // LI already near full; overflow capped
        float nearFull = LargeIntestineComponent.MaxVolumeMl - 1f;
        var (em, _) = BuildSi(chymeVol: 100f, absorptionRate: 100f, residueFrac: 1.0f,
                               withLi: true, liContent: nearFull);
        SiSys.Update(em, deltaTime: 1f);
        Assert.Equal(LargeIntestineComponent.MaxVolumeMl, GetLi(em).ContentVolumeMl, precision: 3);
    }

    [Fact]
    public void SI_NoLI_Component_NoException()
    {
        // SmallIntestineSystem must be resilient when LargeIntestineComponent is absent.
        var (em, _) = BuildSi(chymeVol: 100f, absorptionRate: 10f, withLi: false);
        var ex = Record.Exception(() => SiSys.Update(em, deltaTime: 1f));
        Assert.Null(ex);
    }

    // ===========================================================================
    // LargeIntestineSystem
    // ===========================================================================

    private static LargeIntestineSystem LiSys => new();

    // -- Water reabsorption -----------------------------------------------------

    [Fact]
    public void LI_WaterReabsorption_IncreasesHydration()
    {
        // WaterReabsorptionRate=2, dt=3 → +6 hydration
        var (em, _) = BuildLi(content: 100f, waterRate: 2f, mobilityRate: 0f,
                               withMeta: true, hydration: 50f);
        LiSys.Update(em, deltaTime: 3f);
        Assert.Equal(56f, GetMeta(em).Hydration, precision: 2);
    }

    [Fact]
    public void LI_WaterReabsorption_CappedAt_100()
    {
        var (em, _) = BuildLi(content: 100f, waterRate: 100f, mobilityRate: 0f,
                               withMeta: true, hydration: 95f);
        LiSys.Update(em, deltaTime: 1f);
        Assert.Equal(100f, GetMeta(em).Hydration);
    }

    [Fact]
    public void LI_WaterReabsorption_SkippedWhen_LIIsEmpty()
    {
        var (em, _) = BuildLi(content: 0f, waterRate: 10f, mobilityRate: 0f,
                               withMeta: true, hydration: 50f);
        LiSys.Update(em, deltaTime: 1f);
        Assert.Equal(50f, GetMeta(em).Hydration);
    }

    [Fact]
    public void LI_WaterReabsorption_SkippedWhen_NoMetabolism()
    {
        // Should not throw; content still processed.
        var (em, _) = BuildLi(content: 100f, waterRate: 5f, mobilityRate: 0f, withMeta: false);
        var ex = Record.Exception(() => LiSys.Update(em, deltaTime: 1f));
        Assert.Null(ex);
    }

    // -- Content mobility ------------------------------------------------------

    [Fact]
    public void LI_ContentVolume_ReducedAt_MobilityRate_Times_DeltaTime()
    {
        // processed = 3 * 2 = 6ml; 100 - 6 = 94ml remaining
        var (em, _) = BuildLi(content: 100f, waterRate: 0f, mobilityRate: 3f);
        LiSys.Update(em, deltaTime: 2f);
        Assert.Equal(94f, GetLi(em).ContentVolumeMl, precision: 3);
    }

    [Fact]
    public void LI_ContentVolume_ClampedToZero_WhenMobilityExceedsContent()
    {
        var (em, _) = BuildLi(content: 20f, waterRate: 0f, mobilityRate: 100f);
        LiSys.Update(em, deltaTime: 1f);
        Assert.Equal(0f, GetLi(em).ContentVolumeMl);
    }

    [Fact]
    public void LI_EmptyLI_IsSkipped_NoException()
    {
        var (em, _) = BuildLi(content: 0f, waterRate: 0f, mobilityRate: 5f);
        var ex = Record.Exception(() => LiSys.Update(em, deltaTime: 1f));
        Assert.Null(ex);
    }

    // -- Stool formation → ColonComponent --------------------------------------

    [Fact]
    public void LI_StoolHandoff_AddsToColon_StoolVolume()
    {
        // processed=6ml, stoolFraction=0.6 → stool=3.6ml
        var (em, _) = BuildLi(content: 100f, waterRate: 0f, mobilityRate: 3f,
                               stoolFrac: 0.6f, withColon: true, colonStool: 0f,
                               colonUrge: 100f, colonCap: 200f);
        LiSys.Update(em, deltaTime: 2f);
        Assert.Equal(3.6f, GetColon(em).StoolVolumeMl, precision: 3);
    }

    [Fact]
    public void LI_StoolHandoff_AddedTo_ExistingColonStool()
    {
        // Colon starts at 50ml; receives 3.6ml → 53.6ml
        var (em, _) = BuildLi(content: 100f, waterRate: 0f, mobilityRate: 3f,
                               stoolFrac: 0.6f, withColon: true, colonStool: 50f,
                               colonUrge: 100f, colonCap: 200f);
        LiSys.Update(em, deltaTime: 2f);
        Assert.Equal(53.6f, GetColon(em).StoolVolumeMl, precision: 3);
    }

    [Fact]
    public void LI_StoolHandoff_ClampedAt_ColonCapacity()
    {
        // Colon near-full; overflow capped at capacity
        var (em, _) = BuildLi(content: 100f, waterRate: 0f, mobilityRate: 100f,
                               stoolFrac: 1.0f, withColon: true, colonStool: 195f,
                               colonUrge: 100f, colonCap: 200f);
        LiSys.Update(em, deltaTime: 1f);
        Assert.Equal(200f, GetColon(em).StoolVolumeMl, precision: 3);
    }

    [Fact]
    public void LI_NoColon_Component_NoException()
    {
        var (em, _) = BuildLi(content: 100f, waterRate: 0f, mobilityRate: 5f,
                               stoolFrac: 0.6f, withColon: false);
        var ex = Record.Exception(() => LiSys.Update(em, deltaTime: 1f));
        Assert.Null(ex);
    }

    // ===========================================================================
    // ColonSystem — tag lifecycle
    // ===========================================================================

    private static ColonSystem ColonSys => new();

    [Fact]
    public void Colon_BelowUrgeThreshold_NoTags()
    {
        // 50ml < 100ml urge threshold → no tags
        var (em, entity) = BuildColon(stool: 50f, urge: 100f, cap: 200f);
        ColonSys.Update(em, deltaTime: 1f);
        Assert.False(entity.Has<DefecationUrgeTag>());
        Assert.False(entity.Has<BowelCriticalTag>());
    }

    [Fact]
    public void Colon_AtUrgeThreshold_AppliesUrgeTag()
    {
        // 100ml == 100ml urge threshold → DefecationUrgeTag applied
        var (em, entity) = BuildColon(stool: 100f, urge: 100f, cap: 200f);
        ColonSys.Update(em, deltaTime: 1f);
        Assert.True(entity.Has<DefecationUrgeTag>());
        Assert.False(entity.Has<BowelCriticalTag>());
    }

    [Fact]
    public void Colon_AboveUrgeThreshold_ButBelowCapacity_AppliesUrgeTagOnly()
    {
        // 150ml: urge (100ml) triggered, critical (200ml) not yet
        var (em, entity) = BuildColon(stool: 150f, urge: 100f, cap: 200f);
        ColonSys.Update(em, deltaTime: 1f);
        Assert.True(entity.Has<DefecationUrgeTag>());
        Assert.False(entity.Has<BowelCriticalTag>());
    }

    [Fact]
    public void Colon_AtCapacity_AppliesBothTags()
    {
        // 200ml == 200ml capacity → both tags
        var (em, entity) = BuildColon(stool: 200f, urge: 100f, cap: 200f);
        ColonSys.Update(em, deltaTime: 1f);
        Assert.True(entity.Has<DefecationUrgeTag>());
        Assert.True(entity.Has<BowelCriticalTag>());
    }

    [Fact]
    public void Colon_TagsCleared_WhenVolumeDropsBack_BelowUrge()
    {
        // Start with urge, then drop back below threshold → tag removed
        var (em, entity) = BuildColon(stool: 120f, urge: 100f, cap: 200f);
        entity.Add(new DefecationUrgeTag());    // tag was present from previous tick

        // Now colon empties (simulate defecation)
        var colon = entity.Get<ColonComponent>();
        colon.StoolVolumeMl = 10f;             // well below urge threshold
        entity.Add(colon);

        ColonSys.Update(em, deltaTime: 1f);
        Assert.False(entity.Has<DefecationUrgeTag>());
    }

    [Fact]
    public void Colon_CriticalTag_Cleared_WhenVolumeDropsBelowCapacity()
    {
        var (em, entity) = BuildColon(stool: 200f, urge: 100f, cap: 200f);
        entity.Add(new BowelCriticalTag());

        var colon = entity.Get<ColonComponent>();
        colon.StoolVolumeMl = 150f;            // below capacity, still has urge
        entity.Add(colon);

        ColonSys.Update(em, deltaTime: 1f);
        Assert.False(entity.Has<BowelCriticalTag>());
        Assert.True(entity.Has<DefecationUrgeTag>());  // urge still applies at 150ml
    }

    [Fact]
    public void Colon_EmptyColon_NoTags()
    {
        var (em, entity) = BuildColon(stool: 0f, urge: 100f, cap: 200f);
        ColonSys.Update(em, deltaTime: 1f);
        Assert.False(entity.Has<DefecationUrgeTag>());
        Assert.False(entity.Has<BowelCriticalTag>());
    }

    // ===========================================================================
    // Pipeline integration — one tick through all three systems in order
    // ===========================================================================

    [Fact]
    public void Pipeline_ChymeFlowsFromSI_ThroughLI_IntoColon_InOnePass()
    {
        // Setup: SI has 200ml chyme; LI and Colon start empty.
        // Rates chosen for clear arithmetic.
        var em     = new EntityManager();
        var entity = em.CreateEntity();

        entity.Add(new SmallIntestineComponent
        {
            ChymeVolumeMl        = 200f,
            AbsorptionRate       = 10f,   // processes 10ml/sec
            ResidueToLargeFraction = 0.5f, // 50% of processed → LI
            Chyme                = new NutrientProfile { Fiber = 200f },
        });
        entity.Add(new LargeIntestineComponent
        {
            ContentVolumeMl      = 0f,
            WaterReabsorptionRate = 0f,
            MobilityRate         = 5f,    // processes 5ml/sec once content arrives
            StoolFraction        = 0.6f,
        });
        entity.Add(new ColonComponent
        {
            StoolVolumeMl  = 0f,
            UrgeThresholdMl = 100f,
            CapacityMl     = 200f,
        });
        entity.Add(new MetabolismComponent { Hydration = 50f });

        // One tick, dt=1
        // SI: processed=10ml, ratio=0.05 → LI residue = 10 * 0.5 = 5ml
        SiSys.Update(em, deltaTime: 1f);
        Assert.Equal(190f, entity.Get<SmallIntestineComponent>().ChymeVolumeMl, precision: 2);
        Assert.Equal(5f,   entity.Get<LargeIntestineComponent>().ContentVolumeMl, precision: 2);

        // LI: processed = min(5*1, 5) = 5ml → stool = 5 * 0.6 = 3ml; LI now empty
        LiSys.Update(em, deltaTime: 1f);
        Assert.Equal(0f,  entity.Get<LargeIntestineComponent>().ContentVolumeMl, precision: 2);
        Assert.Equal(3f,  entity.Get<ColonComponent>().StoolVolumeMl, precision: 2);

        // ColonSystem: 3ml < 100ml urge threshold → no tags
        ColonSys.Update(em, deltaTime: 1f);
        Assert.False(entity.Has<DefecationUrgeTag>());
    }

    // ===========================================================================
    // Helpers
    // ===========================================================================

    private static (EntityManager em, Entity entity) BuildSi(
        float chymeVol      = 100f,
        float absorptionRate = 5f,
        float residueFrac   = 0.4f,
        NutrientProfile? chyme = null,
        bool  withLi        = false,
        float liContent     = 0f)
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();

        entity.Add(new SmallIntestineComponent
        {
            ChymeVolumeMl          = chymeVol,
            AbsorptionRate         = absorptionRate,
            ResidueToLargeFraction = residueFrac,
            Chyme                  = chyme ?? default,
        });

        if (withLi)
        {
            entity.Add(new LargeIntestineComponent
            {
                ContentVolumeMl      = liContent,
                WaterReabsorptionRate = 0f,
                MobilityRate         = 0f,
                StoolFraction        = 0f,
            });
        }

        return (em, entity);
    }

    private static (EntityManager em, Entity entity) BuildLi(
        float content      = 100f,
        float waterRate    = 1f,
        float mobilityRate = 3f,
        float stoolFrac    = 0.6f,
        bool  withMeta     = true,
        float hydration    = 50f,
        bool  withColon    = false,
        float colonStool   = 0f,
        float colonUrge    = 100f,
        float colonCap     = 200f)
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();

        entity.Add(new LargeIntestineComponent
        {
            ContentVolumeMl      = content,
            WaterReabsorptionRate = waterRate,
            MobilityRate         = mobilityRate,
            StoolFraction        = stoolFrac,
        });

        if (withMeta)
            entity.Add(new MetabolismComponent { Hydration = hydration });

        if (withColon)
        {
            entity.Add(new ColonComponent
            {
                StoolVolumeMl   = colonStool,
                UrgeThresholdMl = colonUrge,
                CapacityMl      = colonCap,
            });
        }

        return (em, entity);
    }

    private static (EntityManager em, Entity entity) BuildColon(
        float stool = 0f,
        float urge  = 100f,
        float cap   = 200f)
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();

        entity.Add(new ColonComponent
        {
            StoolVolumeMl   = stool,
            UrgeThresholdMl = urge,
            CapacityMl      = cap,
        });

        return (em, entity);
    }

    // -- Data accessors --------------------------------------------------------

    private static SmallIntestineComponent GetSi(EntityManager em) =>
        em.Query<SmallIntestineComponent>().First().Get<SmallIntestineComponent>();

    private static LargeIntestineComponent GetLi(EntityManager em) =>
        em.Query<LargeIntestineComponent>().First().Get<LargeIntestineComponent>();

    private static ColonComponent GetColon(EntityManager em) =>
        em.Query<ColonComponent>().First().Get<ColonComponent>();

    private static MetabolismComponent GetMeta(EntityManager em) =>
        em.Query<MetabolismComponent>().First().Get<MetabolismComponent>();
}
