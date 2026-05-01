using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Transit phase. Advances each <see cref="EsophagusTransitComponent"/> toward the
/// stomach. When progress reaches 1.0, deposits the bolus or liquid into the consumer's
/// <see cref="StomachComponent"/> (volume + queued nutrients) and destroys the transit
/// entity.
/// </summary>
/// <remarks>
/// Reads: <see cref="EsophagusTransitComponent"/>, <see cref="LiquidComponent"/>,
/// <see cref="BolusComponent"/>, <see cref="StomachComponent"/>,
/// <see cref="LifeStateComponent"/> (Deceased consumers do not receive deposits).<br/>
/// Writes: <see cref="EsophagusTransitComponent"/>.Progress, target
/// <see cref="StomachComponent"/>; destroys completed transit entities.<br/>
/// Phase: Transit, after <see cref="InteractionSystem"/>/<see cref="FeedingSystem"/>/<see cref="DrinkingSystem"/>
/// have created transit entities and before <see cref="DigestionSystem"/> drains the stomach.
/// </remarks>
public class EsophagusSystem : ISystem
{
    /// <summary>Per-tick progress and deposit pass.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds).</param>
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

                // WP-3.0.0: Deceased consumers no longer receive deposits; transit entity is still destroyed.
                if (consumer != null && consumer.Has<StomachComponent>() && LifeStateGuard.IsBiologicallyTicking(consumer))
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
