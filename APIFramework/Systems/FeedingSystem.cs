using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

// Hardcoded food source — spawns a banana bolus directly when hunger crosses the threshold.
// Stands in for a real food source (fridge, counter, etc.) until those exist in the world.
public class FeedingSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            var meta = entity.Get<MetabolismComponent>();

            // Hunger is computed as (100 - Satiation); eat when 40% hungry (Satiation < 60)
            if (meta.Hunger < 40f) continue;

            // Severe dehydration takes priority — yield the throat to water
            // TODO: Replace with priority queue when Brain is implemented.
            if (entity.Has<DehydratedTag>()) continue;

            bool throatBusy = em.Query<EsophagusTransitComponent>()
                .Any(t => t.Get<EsophagusTransitComponent>().TargetEntityId == entity.Id);
            if (throatBusy) continue;

            if (entity.Has<StomachComponent>())
            {
                var stomach = entity.Get<StomachComponent>();
                if (stomach.IsFull) continue;
                if (stomach.NutritionQueued >= 70f) continue;
            }

            var bolus = em.CreateEntity();
            bolus.Add(new IdentityComponent("Banana Bolus", "Bolus"));
            bolus.Add(new BolusComponent
            {
                Volume         = 50f,
                NutritionValue = 35f,
                FoodType       = "Banana",
                Toughness      = 0.2f
            });
            bolus.Add(new EsophagusTransitComponent
            {
                Progress       = 0f,
                Speed          = 0.3f,
                TargetEntityId = entity.Id
            });
        }
    }
}
