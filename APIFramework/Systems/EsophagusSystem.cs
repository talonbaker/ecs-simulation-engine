using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Audio;
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
    private readonly SoundTriggerBus?   _soundBus;
    private readonly SimulationClock?   _clock;

    public EsophagusSystem(SoundTriggerBus? soundBus = null, SimulationClock? clock = null)
    {
        _soundBus = soundBus;
        _clock    = clock;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        var entities = em.Query<EsophagusTransitComponent>().ToList();

        foreach (var entity in entities)
        {
            var transit = entity.Get<EsophagusTransitComponent>();
            transit.Progress += transit.Speed * deltaTime;

            if (transit.Progress >= 1.0f)
            {
                var consumer = em.GetAllEntities().FirstOrDefault(e => e.Id == transit.TargetEntityId);

                if (consumer != null && consumer.Has<StomachComponent>() && LifeStateGuard.IsBiologicallyTicking(consumer))
                {
                    var stomach = consumer.Get<StomachComponent>();

                    if (entity.Has<LiquidComponent>())
                    {
                        var liquid = entity.Get<LiquidComponent>();
                        stomach.CurrentVolumeMl  = Math.Min(stomach.CurrentVolumeMl + liquid.VolumeMl, StomachComponent.MaxVolumeMl);
                        stomach.NutrientsQueued += liquid.Nutrients;
                    }
                    else if (entity.Has<BolusComponent>())
                    {
                        var bolus = entity.Get<BolusComponent>();
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

                if (_soundBus != null && _clock != null)
                {
                    var consumer = em.GetAllEntities().FirstOrDefault(e => e.Id == transit.TargetEntityId);
                    if (consumer != null && consumer.Has<PositionComponent>())
                    {
                        var cpos = consumer.Get<PositionComponent>();
                        var kind = entity.Has<BolusComponent>() ? SoundTriggerKind.Chew : SoundTriggerKind.Slurp;
                        _soundBus.Emit(kind, consumer.Id, cpos.X, cpos.Z, 0.5f, (long)_clock.TotalTime);
                    }
                }
            }
        }
    }
}
