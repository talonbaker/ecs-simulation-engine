using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Behavior phase. Spawns a water entity (with <see cref="LiquidComponent"/> +
/// <see cref="EsophagusTransitComponent"/>) when <see cref="DesireType.Drink"/> is the
/// dominant drive, the throat is clear, and the queued hydration is below the cap.
/// Counterpart to <see cref="FeedingSystem"/>.
/// </summary>
/// <remarks>
/// Reads: <see cref="MetabolismComponent"/>, <see cref="DriveComponent"/>,
/// <see cref="StomachComponent"/>, <see cref="DehydratedTag"/>,
/// <see cref="EsophagusTransitComponent"/> (throat-busy check), <see cref="LifeStateComponent"/>.<br/>
/// Writes: spawns new water transit entities only — does not mutate existing components on the drinker.<br/>
/// Phase: Behavior, after <see cref="BrainSystem"/> has picked the dominant drive and
/// before <see cref="EsophagusSystem"/> moves the resulting transit entity.
/// </remarks>
public class DrinkingSystem : ISystem
{
    private readonly DrinkingSystemConfig _cfg;

    /// <summary>Constructs the drinking system with its tuning.</summary>
    /// <param name="cfg">Drinking tuning (water profile, queue caps, esophagus speed).</param>
    public DrinkingSystem(DrinkingSystemConfig cfg) => _cfg = cfg;

    /// <summary>Per-tick action pass; spawns water transit entities for thirsty NPCs whose dominant drive is Drink.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds, unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip Incapacitated/Deceased NPCs

            // Only act if the brain has selected Drink as the dominant drive
            if (!entity.Has<DriveComponent>()) continue;
            if (entity.Get<DriveComponent>().Dominant != DesireType.Drink) continue;

            // Throat must be clear — one thing at a time
            bool throatBusy = em.Query<EsophagusTransitComponent>()
                .Any(t => t.Get<EsophagusTransitComponent>().TargetEntityId == entity.Id);
            if (throatBusy) continue;

            // Don't queue more water than the stomach can absorb soon.
            // v0.7.0+: cap is ml of water pending in the queued nutrient profile.
            // Cap is higher when severely dehydrated so intake keeps pace with need.
            if (entity.Has<StomachComponent>())
            {
                var stomach  = entity.Get<StomachComponent>();
                float cap    = entity.Has<DehydratedTag>()
                    ? _cfg.HydrationQueueCapDehydrated
                    : _cfg.HydrationQueueCap;
                if (stomach.NutrientsQueued.Water >= cap) continue;
            }

            // Spawn a gulp of water into the esophagus
            var water = em.CreateEntity();
            water.Add(new IdentityComponent("Water", "Liquid"));
            water.Add(new LiquidComponent
            {
                VolumeMl   = _cfg.Water.VolumeMl,
                Nutrients  = _cfg.Water.Nutrients,   // full profile — pure water by default
                LiquidType = "Water"
            });
            water.Add(new EsophagusTransitComponent
            {
                Progress       = 0f,
                Speed          = _cfg.Water.EsophagusSpeed,
                TargetEntityId = entity.Id
            });
        }
    }
}
