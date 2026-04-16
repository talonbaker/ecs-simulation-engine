using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Spawns a banana bolus into the esophagus when eating is the dominant drive.
/// Stands in for a real food source (fridge, counter, bowl) until world food
/// entities exist — at that point this system will query for available food
/// rather than conjuring it from nothing.
///
/// Pipeline position: 4 of 8 — after BrainSystem has picked the dominant drive,
/// before InteractionSystem.
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

            // Spawn a banana bolus into the esophagus
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
        }
    }
}
