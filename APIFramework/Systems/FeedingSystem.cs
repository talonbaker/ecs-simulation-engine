using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Feeds the entity when Eat is the dominant drive.
///
/// FOOD SOURCE PRIORITY (in order)
/// ─────────────────────────────────
/// 1. World food sitting loose (BolusComponent, not in transit) — eat immediately.
/// 2. Fridge has food (FridgeComponent.FoodCount > 0):
///    a. Entity is NOT near the fridge → set MovementTargetComponent, wait next tick.
///    b. Entity IS near the fridge → take one banana, transit immediately.
/// 3. Fridge is empty → no action.  Entity accumulates hunger indefinitely (starvation).
///
/// PROXIMITY
/// ─────────
/// "Near the fridge" means the entity's PositionComponent is within ProximityRadius
/// of the fridge's PositionComponent.  If the entity has no PositionComponent the
/// system falls through to the old instant-conjure behaviour so non-spatial entities
/// (tests, CLI) still work correctly.
/// </summary>
public class FeedingSystem : ISystem
{
    private const float ProximityRadius = 1.5f;

    private readonly FeedingSystemConfig _cfg;

    public FeedingSystem(FeedingSystemConfig cfg) => _cfg = cfg;

    public void Update(EntityManager em, float deltaTime)
    {
        // Locate the fridge entity (null if world has none).
        Entity? fridgeEntity = null;
        foreach (var e in em.GetAllEntities())
        {
            if (e.Has<FridgeComponent>()) { fridgeEntity = e; break; }
        }

        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            // Only act if the brain has selected Eat as the dominant drive.
            if (!entity.Has<DriveComponent>()) continue;
            if (entity.Get<DriveComponent>().Dominant != DesireType.Eat) continue;

            var meta = entity.Get<MetabolismComponent>();
            if (meta.Hunger < _cfg.HungerThreshold) continue;

            // Throat must be clear — one bolus at a time.
            bool throatBusy = em.Query<EsophagusTransitComponent>()
                .Any(t => t.Get<EsophagusTransitComponent>().TargetEntityId == entity.Id);
            if (throatBusy) continue;

            // Don't over-queue.
            if (entity.Has<StomachComponent>())
            {
                var stomach = entity.Get<StomachComponent>();
                if (stomach.IsFull) continue;
                if (stomach.NutrientsQueued.Calories >= _cfg.NutritionQueueCap) continue;
            }

            // ── 1. Loose world food (dropped / pre-placed bolus) ─────────────
            var worldFood = em.Query<BolusComponent>()
                .Where(f => !f.Has<EsophagusTransitComponent>() && !f.Has<StoredTag>())
                .ToList();

            if (worldFood.Count > 0)
            {
                SendToEsophagus(em, worldFood[0], entity);
                continue;
            }

            // ── 2. Fridge ─────────────────────────────────────────────────────
            if (fridgeEntity == null)
            {
                // No fridge in this world — fall back to conjure (supports old tests/CLI).
                ConjureBanana(em, entity);
                continue;
            }

            var fridge = fridgeEntity.Get<FridgeComponent>();

            if (fridge.FoodCount <= 0)
            {
                // Fridge is empty — Billy starves.  Nothing to do.
                continue;
            }

            // Does this entity have a spatial position?
            if (!entity.Has<PositionComponent>() || !fridgeEntity.Has<PositionComponent>())
            {
                // Non-spatial mode (tests, CLI) — conjure directly.
                ConjureBanana(em, entity);
                fridge.FoodCount--;
                fridgeEntity.Add(fridge);   // write back (struct)
                continue;
            }

            var entityPos = entity.Get<PositionComponent>();
            var fridgePos = fridgeEntity.Get<PositionComponent>();

            float dx   = entityPos.X - fridgePos.X;
            float dz   = entityPos.Z - fridgePos.Z;
            float dist = MathF.Sqrt(dx * dx + dz * dz);

            if (dist > ProximityRadius)
            {
                // Not at the fridge yet — start/maintain movement toward it.
                // Only add MovementTargetComponent if it isn't already set to the fridge.
                if (!entity.Has<MovementTargetComponent>() ||
                    entity.Get<MovementTargetComponent>().TargetEntityId != fridgeEntity.Id)
                {
                    entity.Add(new MovementTargetComponent
                    {
                        TargetEntityId = fridgeEntity.Id,
                        Label          = "Fridge"
                    });
                }
                // Don't eat yet — wait until Billy arrives.
                continue;
            }

            // Arrived at fridge — grab one banana.
            fridge.FoodCount--;
            fridgeEntity.Add(fridge);   // write back (struct overwrites in-place)

            ConjureBanana(em, entity);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SendToEsophagus(EntityManager em, Entity foodEntity, Entity eater)
    {
        bool isRotten = foodEntity.Has<RotTag>();
        foodEntity.Add(new EsophagusTransitComponent
        {
            Progress       = 0f,
            Speed          = _cfg.Banana.EsophagusSpeed,
            TargetEntityId = eater.Id
        });
        if (isRotten) eater.Add(new ConsumedRottenFoodTag());
    }

    private void ConjureBanana(EntityManager em, Entity eater)
    {
        var banana = _cfg.Banana;
        var bolus  = em.CreateEntity();
        bolus.Add(new IdentityComponent("Banana Bolus", "Bolus"));
        bolus.Add(new BolusComponent
        {
            Volume    = banana.VolumeMl,
            Nutrients = banana.Nutrients,
            FoodType  = "Banana",
            Toughness = banana.Toughness
        });
        bolus.Add(new EsophagusTransitComponent
        {
            Progress       = 0f,
            Speed          = banana.EsophagusSpeed,
            TargetEntityId = eater.Id
        });
        bolus.Add(new RotComponent
        {
            AgeSeconds  = 0f,
            RotLevel    = 0f,
            RotStartAge = _cfg.FoodFreshnessSeconds,
            RotRate     = _cfg.FoodRotRate
        });
    }
}
