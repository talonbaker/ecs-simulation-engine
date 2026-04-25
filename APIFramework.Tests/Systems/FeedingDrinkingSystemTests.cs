using APIFramework.Components;
using Xunit;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;

namespace APIFramework.Tests.Systems;

/// <summary>
/// Unit tests for FeedingSystem and DrinkingSystem — the action layer.
///
/// These systems both follow the same guard pattern before acting:
///   1. Is the dominant drive correct? (Eat / Drink)
///   2. Is the throat clear? (no EsophagusTransitComponent targeting this entity)
///   3. Is the stomach already queued past the cap?
///
/// Only when all guards pass does the system spawn a new entity into the esophagus.
/// Testing these guards explicitly means future changes to the guard logic will
/// fail loudly here rather than silently producing broken sim runs.
/// </summary>
public class FeedingDrinkingSystemTests
{
    // ── Feeding config ─────────────────────────────────────────────────────────

    private static readonly FeedingSystemConfig FeedCfg = new()
    {
        HungerThreshold    = 40f,
        NutritionQueueCap  = 240f,
        FoodFreshnessSeconds = 86_400f,
        FoodRotRate        = 0.001f,
        Banana             = new FoodItemConfig
        {
            VolumeMl         = 50f,
            EsophagusSpeed   = 0.3f,
            Nutrients        = new NutrientProfile { Carbohydrates = 27f, Proteins = 1.3f, Fats = 0.4f, Water = 89f },
        }
    };

    // ── Drinking config ────────────────────────────────────────────────────────

    private static readonly DrinkingSystemConfig DrinkCfg = new()
    {
        HydrationQueueCap           = 15f,
        HydrationQueueCapDehydrated = 30f,
        Water = new DrinkItemConfig
        {
            VolumeMl       = 15f,
            EsophagusSpeed = 0.8f,
            Nutrients      = new NutrientProfile { Water = 15f },
        }
    };

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Creates an entity in a state where FeedingSystem WILL feed it.</summary>
    private static (EntityManager em, Entity eater) ReadyToEat(
        float satiation  = 30f,    // Hunger = 70 — above threshold
        float queuedKcal = 0f)
    {
        var em    = new EntityManager();
        var eater = em.CreateEntity();
        eater.Add(new MetabolismComponent { Satiation = satiation, Hydration = 80f });
        eater.Add(new DriveComponent      { EatUrgency = 0.7f, DrinkUrgency = 0.2f });
        eater.Add(new StomachComponent
        {
            CurrentVolumeMl = 0f,
            DigestionRate   = 1f,
            NutrientsQueued = new NutrientProfile { Carbohydrates = queuedKcal / 4f }, // rough kcal via carbs
        });
        return (em, eater);
    }

    /// <summary>Creates an entity ready to drink.</summary>
    private static (EntityManager em, Entity drinker) ReadyToDrink(
        float hydration    = 30f,  // Thirst = 70
        float queuedWater  = 0f,
        bool  dehydrated   = false)
    {
        var em      = new EntityManager();
        var drinker = em.CreateEntity();
        drinker.Add(new MetabolismComponent { Satiation = 80f, Hydration = hydration });
        drinker.Add(new DriveComponent      { DrinkUrgency = 0.7f, EatUrgency = 0.2f });
        drinker.Add(new StomachComponent
        {
            CurrentVolumeMl = 0f,
            DigestionRate   = 1f,
            NutrientsQueued = new NutrientProfile { Water = queuedWater },
        });
        if (dehydrated) drinker.Add(new DehydratedTag());
        return (em, drinker);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  FEEDING SYSTEM
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FeedingSystem_SpawnsBolus_WhenEatIsDominant()
    {
        var (em, eater) = ReadyToEat();
        var sys = new FeedingSystem(FeedCfg);

        sys.Update(em, deltaTime: 1f);

        // A bolus entity should have been spawned (it has BolusComponent and
        // EsophagusTransitComponent targeting the eater).
        var bolusInTransit = em.Query<EsophagusTransitComponent>()
            .Where(e => e.Get<EsophagusTransitComponent>().TargetEntityId == eater.Id)
            .ToList();

        Assert.Single(bolusInTransit);
    }

    [Fact]
    public void FeedingSystem_DoesNotSpawn_WhenDominantIsNotEat()
    {
        var (em, eater) = ReadyToEat();
        // Override: make Drink the dominant drive
        eater.Add(new DriveComponent { DrinkUrgency = 0.9f, EatUrgency = 0.2f });

        new FeedingSystem(FeedCfg).Update(em, deltaTime: 1f);

        Assert.Empty(em.Query<EsophagusTransitComponent>());
    }

    [Fact]
    public void FeedingSystem_DoesNotSpawn_WhenHungerBelowThreshold()
    {
        // Hunger threshold = 40. Satiation=65 → Hunger=35 < 40.
        var (em, eater) = ReadyToEat(satiation: 65f);

        new FeedingSystem(FeedCfg).Update(em, deltaTime: 1f);

        Assert.Empty(em.Query<EsophagusTransitComponent>());
    }

    [Fact]
    public void FeedingSystem_DoesNotSpawn_WhenThroatBusy()
    {
        var (em, eater) = ReadyToEat();

        // Manually place something already in transit to this entity
        var existing = em.CreateEntity();
        existing.Add(new EsophagusTransitComponent { TargetEntityId = eater.Id, Progress = 0.5f });

        new FeedingSystem(FeedCfg).Update(em, deltaTime: 1f);

        // Should still only be the one we manually created
        var inTransit = em.Query<EsophagusTransitComponent>()
            .Where(e => e.Get<EsophagusTransitComponent>().TargetEntityId == eater.Id)
            .ToList();
        Assert.Single(inTransit);
    }

    [Fact]
    public void FeedingSystem_DoesNotSpawn_WhenStomachIsFull()
    {
        var (em, eater) = ReadyToEat();
        // Overfill the stomach (max = 1000ml)
        eater.Add(new StomachComponent { CurrentVolumeMl = 1000f, DigestionRate = 1f });

        new FeedingSystem(FeedCfg).Update(em, deltaTime: 1f);

        Assert.Empty(em.Query<EsophagusTransitComponent>());
    }

    [Fact]
    public void FeedingSystem_DoesNotSpawn_WhenNutritionQueueCapReached()
    {
        // NutritionQueueCap = 240 kcal. Load stomach with 300 kcal of carbs (75g @ 4kcal/g).
        var (em, eater) = ReadyToEat();
        eater.Add(new StomachComponent
        {
            CurrentVolumeMl = 100f,
            DigestionRate   = 1f,
            NutrientsQueued = new NutrientProfile { Carbohydrates = 75f }, // 300 kcal
        });

        new FeedingSystem(FeedCfg).Update(em, deltaTime: 1f);

        // Only thing in the esophagus should be nothing new
        Assert.Empty(em.Query<EsophagusTransitComponent>());
    }

    [Fact]
    public void FeedingSystem_SpawnedBolus_HasBolusComponent()
    {
        var (em, eater) = ReadyToEat();
        new FeedingSystem(FeedCfg).Update(em, deltaTime: 1f);

        var bolus = em.Query<BolusComponent>().FirstOrDefault();
        Assert.NotNull(bolus);
        Assert.True(bolus!.Has<BolusComponent>());
    }

    [Fact]
    public void FeedingSystem_ConsumedRottenFoodTag_WhenWorldFoodIsRotten()
    {
        var (em, eater) = ReadyToEat();

        // Place a rotten food entity in the world
        var rottenFood = em.CreateEntity();
        rottenFood.Add(new BolusComponent { Volume = 50f, FoodType = "Banana" });
        rottenFood.Add(new RotTag()); // rotten

        new FeedingSystem(FeedCfg).Update(em, deltaTime: 1f);

        Assert.True(eater.Has<ConsumedRottenFoodTag>());
    }

    [Fact]
    public void FeedingSystem_PrefersWorldFood_OverConjuring()
    {
        var (em, eater) = ReadyToEat();

        // One existing food entity in the world
        var worldFood = em.CreateEntity();
        worldFood.Add(new BolusComponent { Volume = 50f, FoodType = "Banana" });

        new FeedingSystem(FeedCfg).Update(em, deltaTime: 1f);

        // The world food should now have a transit component (eaten)
        // and no new bolus should have been conjured
        Assert.True(worldFood.Has<EsophagusTransitComponent>());

        // Count bolus entities — should only be the one we placed
        Assert.Single(em.Query<BolusComponent>().ToList());
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  DRINKING SYSTEM
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DrinkingSystem_SpawnsWater_WhenDrinkIsDominant()
    {
        var (em, drinker) = ReadyToDrink();
        new DrinkingSystem(DrinkCfg).Update(em, deltaTime: 1f);

        var inTransit = em.Query<EsophagusTransitComponent>()
            .Where(e => e.Get<EsophagusTransitComponent>().TargetEntityId == drinker.Id)
            .ToList();

        Assert.Single(inTransit);
    }

    [Fact]
    public void DrinkingSystem_DoesNotSpawn_WhenDominantIsNotDrink()
    {
        var (em, drinker) = ReadyToDrink();
        drinker.Add(new DriveComponent { EatUrgency = 0.9f, DrinkUrgency = 0.2f });

        new DrinkingSystem(DrinkCfg).Update(em, deltaTime: 1f);

        Assert.Empty(em.Query<EsophagusTransitComponent>());
    }

    [Fact]
    public void DrinkingSystem_DoesNotSpawn_WhenThroatBusy()
    {
        var (em, drinker) = ReadyToDrink();

        var existing = em.CreateEntity();
        existing.Add(new EsophagusTransitComponent { TargetEntityId = drinker.Id, Progress = 0.3f });

        new DrinkingSystem(DrinkCfg).Update(em, deltaTime: 1f);

        var inTransit = em.Query<EsophagusTransitComponent>()
            .Where(e => e.Get<EsophagusTransitComponent>().TargetEntityId == drinker.Id)
            .Count();
        Assert.Equal(1, inTransit); // only the manually created one
    }

    [Fact]
    public void DrinkingSystem_DoesNotSpawn_WhenWaterQueueCapReached()
    {
        // HydrationQueueCap = 15ml (one gulp). Already 15ml queued → skip.
        var (em, drinker) = ReadyToDrink(queuedWater: 15f);

        new DrinkingSystem(DrinkCfg).Update(em, deltaTime: 1f);

        Assert.Empty(em.Query<EsophagusTransitComponent>());
    }

    [Fact]
    public void DrinkingSystem_UsesHigherCap_WhenDehydrated()
    {
        // DehydratedTag → cap = 30ml. 15ml queued < 30ml → should spawn.
        var (em, drinker) = ReadyToDrink(queuedWater: 15f, dehydrated: true);

        new DrinkingSystem(DrinkCfg).Update(em, deltaTime: 1f);

        var inTransit = em.Query<EsophagusTransitComponent>()
            .Where(e => e.Get<EsophagusTransitComponent>().TargetEntityId == drinker.Id)
            .ToList();

        Assert.Single(inTransit);
    }

    [Fact]
    public void DrinkingSystem_StillBlockedAtHigherCap_WhenDehydrated()
    {
        // DehydratedTag → cap = 30ml. Already 30ml queued → skip.
        var (em, drinker) = ReadyToDrink(queuedWater: 30f, dehydrated: true);

        new DrinkingSystem(DrinkCfg).Update(em, deltaTime: 1f);

        Assert.Empty(em.Query<EsophagusTransitComponent>());
    }

    [Fact]
    public void DrinkingSystem_SpawnedEntity_HasLiquidComponent()
    {
        var (em, _) = ReadyToDrink();
        new DrinkingSystem(DrinkCfg).Update(em, deltaTime: 1f);

        Assert.NotEmpty(em.Query<LiquidComponent>());
    }
}
