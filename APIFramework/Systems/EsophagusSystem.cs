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
                        // Physical volume enters the stomach; hydration waits to be absorbed by digestion
                        stomach.CurrentVolumeMl  = Math.Min(stomach.CurrentVolumeMl + liquid.VolumeMl, StomachComponent.MaxVolumeMl);
                        stomach.HydrationQueued += liquid.HydrationValue;
                    }
                    else if (entity.Has<BolusComponent>())
                    {
                        var bolus = entity.Get<BolusComponent>();
                        // Physical volume enters the stomach; nutrition waits to be absorbed by digestion
                        stomach.CurrentVolumeMl  = Math.Min(stomach.CurrentVolumeMl + bolus.Volume, StomachComponent.MaxVolumeMl);
                        stomach.NutritionQueued += bolus.NutritionValue;
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
