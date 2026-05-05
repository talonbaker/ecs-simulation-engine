using APIFramework.Components;
using Xunit;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;

namespace APIFramework.Tests.Systems;

/// <summary>
/// Unit tests for DigestionSystem — the pipeline contract between the stomach
/// and the body's nutrient stores.
///
/// --------------------------------------------------------------------------
/// WHY THIS TEST FILE IS THE MOST IMPORTANT ONE
/// --------------------------------------------------------------------------
/// The v0.7.2 post-release investigation found a bug where the simulation ran
/// for hours with no feeding events because StomachComponent filled with volume
/// but zero nutrients. DigestionSystem's contract is:
///
///   FOR EVERY TICK:
///   1. digested  = min(DigestionRate * dt, CurrentVolumeMl)     — how much empties
///   2. ratio     = digested / CurrentVolumeMl                   — fraction released
///   3. released  = NutrientsQueued * ratio                      — proportional slice
///   4. stomach.CurrentVolumeMl -= digested                      — drain volume
///   5. stomach.NutrientsQueued -= released                      — drain nutrients
///   6. meta.NutrientStores     += released                      — accumulate
///   7. meta.Satiation += released.Calories * SatiationPerCalorie
///   8. meta.Hydration += released.Water    * HydrationPerMl
///
/// These tests pin every step of that contract so any future refactor that
/// breaks the pipeline fails loudly here before it reaches a sim run.
/// --------------------------------------------------------------------------
/// </summary>
public class DigestionSystemTests
{
    // -- Standard config --------------------------------------------------------

    private static readonly DigestionSystemConfig Cfg = new()
    {
        SatiationPerCalorie = 0.3f,
        HydrationPerMl      = 2.0f,
    };

    private static DigestionSystem Sys => new(Cfg);

    // -- Helpers ----------------------------------------------------------------

    /// <summary>
    /// Creates an entity with a stomach and optional metabolism component.
    /// All values are explicit — no hidden defaults. This makes each test's
    /// setup a complete specification of the scenario.
    /// </summary>
    private static (EntityManager em, Entity entity) Build(
        float volumeMl        = 100f,
        float digestionRate   = 10f,
        NutrientProfile? nutrients  = null,
        float satiation       = 50f,
        float hydration       = 50f,
        bool  withMetabolism  = true)
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();

        entity.Add(new StomachComponent
        {
            CurrentVolumeMl = volumeMl,
            DigestionRate   = digestionRate,
            NutrientsQueued = nutrients ?? default,
        });

        if (withMetabolism)
        {
            entity.Add(new MetabolismComponent
            {
                Satiation = satiation,
                Hydration = hydration,
            });
        }

        return (em, entity);
    }

    // -- Step 1: Volume drainage ------------------------------------------------

    [Fact]
    public void Volume_ReducedBy_DigestionRate_Times_DeltaTime()
    {
        // digested = 10 * 2 = 20ml; 100 - 20 = 80ml remaining
        var (em, entity) = Build(volumeMl: 100f, digestionRate: 10f);
        Sys.Update(em, deltaTime: 2f);
        Assert.Equal(80f, entity.Get<StomachComponent>().CurrentVolumeMl, precision: 3);
    }

    [Fact]
    public void Volume_ClampedToZero_WhenDigestionExceedsContent()
    {
        // DigestionRate*dt = 100 ml, stomach has only 30 ml → clamp to 0.
        var (em, entity) = Build(volumeMl: 30f, digestionRate: 100f);
        Sys.Update(em, deltaTime: 1f);
        Assert.Equal(0f, entity.Get<StomachComponent>().CurrentVolumeMl);
    }

    [Fact]
    public void EmptyStomach_IsSkipped_NoException()
    {
        var (em, entity) = Build(volumeMl: 0f);
        var ex = Record.Exception(() => Sys.Update(em, deltaTime: 1f));
        Assert.Null(ex);
    }

    [Fact]
    public void EmptyStomach_DoesNot_ChangeSatiation()
    {
        var (em, entity) = Build(volumeMl: 0f, satiation: 50f);
        Sys.Update(em, deltaTime: 5f);
        Assert.Equal(50f, entity.Get<MetabolismComponent>().Satiation);
    }

    // -- Step 2–3: Proportional nutrient release --------------------------------

    [Fact]
    public void Nutrients_Released_ProportionalTo_VolumeDigested()
    {
        // volume=100ml, digestionRate=10, dt=1 → digested=10ml, ratio=0.1
        // Carbs=100g * 0.1 = 10g released; 90g remaining
        var nutrients = new NutrientProfile { Carbohydrates = 100f };
        var (em, entity) = Build(volumeMl: 100f, digestionRate: 10f, nutrients: nutrients);

        Sys.Update(em, deltaTime: 1f);

        var stomach = entity.Get<StomachComponent>();
        Assert.Equal(90f, stomach.NutrientsQueued.Carbohydrates, precision: 2);
    }

    [Fact]
    public void Water_Released_ProportionalTo_VolumeDigested()
    {
        // volume=50ml, water=50ml, digestionRate=5, dt=2 → digested=10, ratio=0.2
        // Released water = 50 * 0.2 = 10ml; remaining = 40ml
        var nutrients = new NutrientProfile { Water = 50f };
        var (em, entity) = Build(volumeMl: 50f, digestionRate: 5f, nutrients: nutrients);

        Sys.Update(em, deltaTime: 2f);

        Assert.Equal(40f, entity.Get<StomachComponent>().NutrientsQueued.Water, precision: 2);
    }

    [Fact]
    public void AllNutrientFields_ReleasedProportionally()
    {
        // Every field in NutrientProfile should shrink by the same ratio.
        // ratio = 10/100 = 0.1 → each field reduced by 10%.
        var nutrients = new NutrientProfile
        {
            Carbohydrates = 10f,
            Proteins      = 5f,
            Fats          = 2f,
            Water         = 20f,
            VitaminC      = 100f,
            Potassium     = 400f,
        };
        var (em, entity) = Build(volumeMl: 100f, digestionRate: 10f, nutrients: nutrients);

        Sys.Update(em, deltaTime: 1f); // ratio = 0.1

        var q = entity.Get<StomachComponent>().NutrientsQueued;
        Assert.Equal(9f,   q.Carbohydrates, precision: 2);
        Assert.Equal(4.5f, q.Proteins,      precision: 2);
        Assert.Equal(1.8f, q.Fats,          precision: 2);
        Assert.Equal(18f,  q.Water,         precision: 2);
        Assert.Equal(90f,  q.VitaminC,      precision: 2);
        Assert.Equal(360f, q.Potassium,     precision: 2);
    }

    // -- Step 7: Satiation gain -------------------------------------------------

    [Fact]
    public void Satiation_Increases_By_ReleasedCalories_Times_Factor()
    {
        // volume=100, digestionRate=10, dt=1 → ratio=0.1
        // Carbs=100g → Calories=400kcal → released=40kcal
        // satiationGain = 40 * 0.3 = 12
        // Satiation: 50 + 12 = 62
        var nutrients = new NutrientProfile { Carbohydrates = 100f };
        var (em, entity) = Build(volumeMl: 100f, digestionRate: 10f, nutrients: nutrients, satiation: 50f);

        Sys.Update(em, deltaTime: 1f);

        Assert.Equal(62f, entity.Get<MetabolismComponent>().Satiation, precision: 2);
    }

    [Fact]
    public void Satiation_CappedAt_100()
    {
        // High-calorie stomach filling a nearly-full entity.
        var nutrients = new NutrientProfile { Carbohydrates = 1000f }; // 4000 kcal queued
        var (em, entity) = Build(volumeMl: 100f, digestionRate: 100f, nutrients: nutrients, satiation: 95f);

        Sys.Update(em, deltaTime: 1f);

        Assert.Equal(100f, entity.Get<MetabolismComponent>().Satiation);
    }

    [Fact]
    public void Satiation_Unchanged_When_NoCalories_InNutrients()
    {
        // Pure water — no macros → no satiation gain.
        var nutrients = new NutrientProfile { Water = 200f };
        var (em, entity) = Build(volumeMl: 100f, digestionRate: 10f, nutrients: nutrients, satiation: 50f);

        Sys.Update(em, deltaTime: 1f);

        Assert.Equal(50f, entity.Get<MetabolismComponent>().Satiation);
    }

    // -- Step 8: Hydration gain -------------------------------------------------

    [Fact]
    public void Hydration_Increases_By_ReleasedWater_Times_Factor()
    {
        // volume=50ml, water=50ml, digestionRate=5, dt=2 → digested=10, ratio=0.2
        // released.Water = 50 * 0.2 = 10ml
        // hydrationGain = 10 * 2.0 = 20
        // Hydration: 30 + 20 = 50
        var nutrients = new NutrientProfile { Water = 50f };
        var (em, entity) = Build(volumeMl: 50f, digestionRate: 5f, nutrients: nutrients, hydration: 30f);

        Sys.Update(em, deltaTime: 2f);

        Assert.Equal(50f, entity.Get<MetabolismComponent>().Hydration, precision: 2);
    }

    [Fact]
    public void Hydration_CappedAt_100()
    {
        var nutrients = new NutrientProfile { Water = 1000f };
        var (em, entity) = Build(volumeMl: 100f, digestionRate: 100f, nutrients: nutrients, hydration: 90f);

        Sys.Update(em, deltaTime: 1f);

        Assert.Equal(100f, entity.Get<MetabolismComponent>().Hydration);
    }

    [Fact]
    public void Hydration_Unchanged_When_NoWater_InNutrients()
    {
        // Pure carbs — no water → no hydration gain.
        var nutrients = new NutrientProfile { Carbohydrates = 100f };
        var (em, entity) = Build(volumeMl: 100f, digestionRate: 10f, nutrients: nutrients, hydration: 40f);

        Sys.Update(em, deltaTime: 1f);

        Assert.Equal(40f, entity.Get<MetabolismComponent>().Hydration);
    }

    // -- Step 6: NutrientStores accumulation ------------------------------------

    [Fact]
    public void NutrientStores_AccumulatesReleasedNutrients()
    {
        // Released Carbs = 100 * 0.1 = 10g; MetabolismComponent starts with 0.
        var nutrients = new NutrientProfile { Carbohydrates = 100f };
        var (em, entity) = Build(volumeMl: 100f, digestionRate: 10f, nutrients: nutrients);

        Sys.Update(em, deltaTime: 1f);

        Assert.Equal(10f, entity.Get<MetabolismComponent>().NutrientStores.Carbohydrates, precision: 2);
    }

    [Fact]
    public void NutrientStores_AreAdditive_AcrossMultipleTicks()
    {
        // Two ticks, each digesting 10ml of 100ml.
        // Tick 1: ratio=0.1, released carbs=10g, remaining=90g, volume=90ml
        // Tick 2: ratio=10/90≈0.111, released carbs≈10g, stores≈20g
        var nutrients = new NutrientProfile { Carbohydrates = 100f };
        var (em, entity) = Build(volumeMl: 100f, digestionRate: 10f, nutrients: nutrients);

        Sys.Update(em, deltaTime: 1f);
        Sys.Update(em, deltaTime: 1f);

        // Each tick releases exactly 10% of the current content by design.
        // After 2 ticks: stores = 10 + 9 = 19g (ratio changes because volume changed)
        float stored = entity.Get<MetabolismComponent>().NutrientStores.Carbohydrates;
        Assert.True(stored > 15f && stored < 25f,
            $"Expected ~19g accumulated carbs after 2 ticks, got {stored:F2}g");
    }

    // -- Regression: the exact bug found in v0.7.2 testing ---------------------

    [Fact]
    public void ZeroNutrients_StomachEmpties_ButNoSatiationOrHydration()
    {
        // This is the bug scenario: volume is present but NutrientsQueued is all zeros
        // (as happens if NutrientProfile fields weren't deserialized). The stomach
        // should still drain, but no satiation or hydration should appear.
        var (em, entity) = Build(
            volumeMl:      100f,
            digestionRate: 10f,
            nutrients:     new NutrientProfile(), // all zeros
            satiation:     50f,
            hydration:     30f);

        Sys.Update(em, deltaTime: 1f);

        Assert.Equal(90f, entity.Get<StomachComponent>().CurrentVolumeMl, precision: 2); // still drains
        Assert.Equal(50f, entity.Get<MetabolismComponent>().Satiation);  // unchanged
        Assert.Equal(30f, entity.Get<MetabolismComponent>().Hydration);  // unchanged
    }

    [Fact]
    public void CorrectNutrients_ProduceSatiation_AndHydration()
    {
        // This is the passing case after the IncludeFields = true fix.
        // A banana-like profile should produce measurable satiation each tick.
        // banana: 27g carbs, 1.3g protein, 0.4g fat, 89ml water
        // Calories = 27*4 + 1.3*4 + 0.4*9 = 108 + 5.2 + 3.6 = 116.8 kcal
        var banana = new NutrientProfile
        {
            Carbohydrates = 27f,
            Proteins      = 1.3f,
            Fats          = 0.4f,
            Water         = 89f,
        };
        var (em, entity) = Build(
            volumeMl:      100f,
            digestionRate: 100f, // digest all in one tick
            nutrients:     banana,
            satiation:     50f,
            hydration:     30f);

        Sys.Update(em, deltaTime: 1f); // ratio = 1.0 — full release

        var meta = entity.Get<MetabolismComponent>();

        // All calories absorbed in one tick:
        // satiationGain = 116.8 * 0.3 = 35.04
        Assert.True(meta.Satiation > 80f,
            $"Expected satiation > 80 after full banana absorption, got {meta.Satiation:F1}");

        // All water absorbed:
        // hydrationGain = 89 * 2.0 = 178 → capped at 100
        Assert.Equal(100f, meta.Hydration);
    }

    // -- No MetabolismComponent: stomach drains but no metabolism update ---------

    [Fact]
    public void StomachDrains_EvenWithout_MetabolismComponent()
    {
        var (em, entity) = Build(
            volumeMl:      100f,
            digestionRate: 10f,
            withMetabolism: false);

        // Should not throw; stomach still empties.
        Sys.Update(em, deltaTime: 1f);

        Assert.Equal(90f, entity.Get<StomachComponent>().CurrentVolumeMl, precision: 3);
    }
}
