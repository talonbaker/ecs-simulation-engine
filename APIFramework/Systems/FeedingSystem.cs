using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Feeds the entity when Eat is the dominant drive.
///
/// FOOD SOURCE PRIORITY
/// ─────────────────────
/// 1. World food entities already present (BolusComponent, not in transit):
///    a. If the food is rotten (RotTag) — eat it anyway, but apply
///       ConsumedRottenFoodTag to the eater. MoodSystem will spike Disgust.
///    b. If fresh — eat normally.
/// 2. No world food exists → conjure a fresh banana bolus (stand-in until
///    a real world/inventory system exists). The spawned bolus carries a
///    RotComponent so it WILL decay if it ever sits uneaten.
///
/// Pipeline position: 7 — after BrainSystem has picked the dominant drive.
/// </summary>
public class FeedingSystem : ISystem
{
    private readonly FeedingSystemConfig _cfg;

    public FeedingSystem(FeedingSystemConfig cfg) => _cfg = cfg;

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            // Only act if the brain has selected Eat as the dominant drive
            if (!entity.Has<DriveComponent>()) continue;
            if (entity.Get<DriveComponent>().Dominant != DesireType.Eat) continue;

            var meta = entity.Get<MetabolismComponent>();

            // Don't eat if hunger is below the meaningful threshold
            if (meta.Hunger < _cfg.HungerThreshold) continue;

            // Throat must be clear — one thing at a time
            bool throatBusy = em.Query<EsophagusTransitComponent>()
                .Any(t => t.Get<EsophagusTransitComponent>().TargetEntityId == entity.Id);
            if (throatBusy) continue;

            // Don't queue more food than the stomach can digest soon
            if (entity.Has<StomachComponent>())
            {
                var stomach = entity.Get<StomachComponent>();
                if (stomach.IsFull) continue;
                if (stomach.NutritionQueued >= _cfg.NutritionQueueCap) continue;
            }

            // ── Look for world food entities sitting in the world (not in transit) ──
            // A world food entity is any entity with BolusComponent but no active transit.
            var worldFood = em.Query<BolusComponent>()
                .Where(f => !f.Has<EsophagusTransitComponent>())
                .ToList();

            if (worldFood.Count > 0)
            {
                // Prefer the first available; rotten food is eaten "by accident"
                var foodEntity = worldFood[0];
                bool isRotten  = foodEntity.Has<RotTag>();

                // Send it down the esophagus
                foodEntity.Add(new EsophagusTransitComponent
                {
                    Progress       = 0f,
                    Speed          = _cfg.Banana.EsophagusSpeed, // use configured speed
                    TargetEntityId = entity.Id
                });

                // Signal the eater if the food was bad — MoodSystem handles the spike
                if (isRotten)
                    entity.Add(new ConsumedRottenFoodTag());
            }
            else
            {
                // ── No world food — conjure a fresh banana (temporary stand-in) ──
                var banana = _cfg.Banana;
                var bolus  = em.CreateEntity();
                bolus.Add(new IdentityComponent("Banana Bolus", "Bolus"));
                bolus.Add(new BolusComponent
                {
                    Volume         = banana.VolumeMl,
                    NutritionValue = banana.NutritionValue,
                    FoodType       = "Banana",
                    Toughness      = banana.Toughness
                });
                bolus.Add(new EsophagusTransitComponent
                {
                    Progress       = 0f,
                    Speed          = banana.EsophagusSpeed,
                    TargetEntityId = entity.Id
                });
                // Give the bolus a rot clock — it WILL decay if it sat around uneaten.
                // In practice it goes straight into transit so it won't reach RotTag,
                // but the component is there for future world-placement scenarios.
                bolus.Add(new RotComponent
                {
                    AgeSeconds  = 0f,
                    RotLevel    = 0f,
                    RotStartAge = _cfg.FoodFreshnessSeconds,
                    RotRate     = _cfg.FoodRotRate
                });
            }
        }
    }
}
