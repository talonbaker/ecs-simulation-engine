using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

public class EsophagusSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        var entities = em.Query<EsophagusTransitComponent>().ToList();

        foreach (var entity in entities)
        {
            var transit = entity.Get<EsophagusTransitComponent>();
            transit.Progress += transit.Speed * deltaTime;

            if (transit.Progress >= 1.0f)
            {
                // 1. Find the Human/Cat that swallowed this
                var consumer = em.GetAllEntities().FirstOrDefault(e => e.Id == transit.TargetEntityId);

                if (consumer != null && consumer.Has<MetabolismComponent>())
                {
                    var meta = consumer.Get<MetabolismComponent>();

                    if (entity.Has<LiquidComponent>())
                    {
                        var liquid = entity.Get<LiquidComponent>();
                        meta.Thirst -= liquid.HydrationValue; // Satisfies Thirst
                    }
                    else if (entity.Has<BolusComponent>())
                    {
                        var bolus = entity.Get<BolusComponent>();
                        meta.Hunger -= bolus.NutritionValue; // Satisfies Hunger
                    }

                    // Clamp values so they don't go negative
                    if (meta.Thirst < 0) meta.Thirst = 0;
                    if (meta.Hunger < 0) meta.Hunger = 0;

                    consumer.Add(meta);
                }

                em.DestroyEntity(entity);
            }
            else
            {
                entity.Add(transit); // Use .Set instead of .Add for updates
            }
        }
    }
}