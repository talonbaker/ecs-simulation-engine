using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Spawns a water entity into the esophagus when drinking is the dominant drive.
/// This is the drinking counterpart to FeedingSystem.
///
/// Previously this logic lived inside BiologicalConditionSystem, which mixed
/// condition-tagging (observation) with water-spawning (action). Separating them
/// means each system has a single, readable responsibility.
///
/// Pipeline position: 5 of 8 — after BrainSystem has picked the dominant drive,
/// before InteractionSystem.
/// </summary>
public class DrinkingSystem : ISystem
{
    private readonly DrinkingSystemConfig _cfg;

    public DrinkingSystem(DrinkingSystemConfig cfg) => _cfg = cfg;

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            // Only act if the brain has selected Drink as the dominant drive
            if (!entity.Has<DriveComponent>()) continue;
            if (entity.Get<DriveComponent>().Dominant != DriveType.Drink) continue;

            // Throat must be clear — one thing at a time
            bool throatBusy = em.Query<EsophagusTransitComponent>()
                .Any(t => t.Get<EsophagusTransitComponent>().TargetEntityId == entity.Id);
            if (throatBusy) continue;

            // Don't queue more water than the stomach can absorb soon.
            // Cap is higher when severely dehydrated so intake keeps pace with need.
            if (entity.Has<StomachComponent>())
            {
                var stomach  = entity.Get<StomachComponent>();
                float cap    = entity.Has<DehydratedTag>()
                    ? _cfg.HydrationQueueCapDehydrated
                    : _cfg.HydrationQueueCap;
                if (stomach.HydrationQueued >= cap) continue;
            }

            // Spawn a gulp of water into the esophagus
            var water = em.CreateEntity();
            water.Add(new IdentityComponent("Water", "Liquid"));
            water.Add(new LiquidComponent
            {
                VolumeMl       = _cfg.Water.VolumeMl,
                HydrationValue = _cfg.Water.HydrationValue,
                LiquidType     = "Water"
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
