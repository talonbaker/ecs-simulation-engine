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
                // Find the consumer this bolus/liquid was headed for
                var consumer = em.GetAllEntities().FirstOrDefault(e => e.Id == transit.TargetEntityId);

                if (consumer != null && consumer.Has<StomachComponent>())
                {
                    var stomach = consumer.Get<StomachComponent>();

                    if (entity.Has<LiquidComponent>())
                    {
                        var liquid = entity.Get<LiquidComponent>();
                        // Physical volume enters the stomach; the full nutrient profile
                        // (water + any dissolved macros/minerals) waits in NutrientsQueued
                        // to be absorbed by DigestionSystem.
                        stomach.CurrentVolumeMl  = Math.Min(stomach.CurrentVolumeMl + liquid.VolumeMl, StomachComponent.MaxVolumeMl);
                        stomach.NutrientsQueued += liquid.Nutrients;
                    }
                    else if (entity.Has<BolusComponent>())
                    {
                        var bolus = entity.Get<BolusComponent>();
                        // Physical volume enters the stomach; the full nutrient profile
                        // (macros, water, vitamins, minerals) waits in NutrientsQueued
                        // to be absorbed by DigestionSystem.
                        stomach.CurrentVolumeMl  = Math.Min(stomach.CurrentVolumeMl + bolus.Volume, StomachComponent.MaxVolumeMl);
                        stomach.NutrientsQueued += bolus.Nutrients;
                    }

                    consumer.Add(stomach);
                }

                em.DestroyEntity(entity);
            }
            else
            {
                entity.Add(transit);
            }
        }
    }
}
