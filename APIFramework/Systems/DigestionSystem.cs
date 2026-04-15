using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

// Drains the stomach over time and releases queued nutrition/hydration into metabolism.
// This is the final step in the eating pipeline:
//   FeedingSystem → EsophagusSystem → StomachComponent → DigestionSystem → MetabolismComponent
public class DigestionSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<StomachComponent>().ToList())
        {
            var stomach = entity.Get<StomachComponent>();
            if (stomach.IsEmpty) continue;

            // How much volume is broken down this tick
            float digested = MathF.Min(stomach.DigestionRate * deltaTime, stomach.CurrentVolumeMl);

            // Proportion of total stomach content digested this tick —
            // nutrients are released at the same rate as volume drains
            float ratio = digested / stomach.CurrentVolumeMl;

            float nutritionReleased  = stomach.NutritionQueued  * ratio;
            float hydrationReleased  = stomach.HydrationQueued  * ratio;

            stomach.CurrentVolumeMl -= digested;
            stomach.NutritionQueued -= nutritionReleased;
            stomach.HydrationQueued -= hydrationReleased;

            if (stomach.CurrentVolumeMl < 0f) stomach.CurrentVolumeMl = 0f;
            if (stomach.NutritionQueued  < 0f) stomach.NutritionQueued  = 0f;
            if (stomach.HydrationQueued  < 0f) stomach.HydrationQueued  = 0f;

            entity.Add(stomach);

            if (!entity.Has<MetabolismComponent>()) continue;

            var meta = entity.Get<MetabolismComponent>();

            // Fill the physiological resources — Hunger and Thirst drop automatically
            // because they are computed as (100 - Satiation) and (100 - Hydration)
            meta.Satiation = MathF.Min(100f, meta.Satiation + nutritionReleased);
            meta.Hydration = MathF.Min(100f, meta.Hydration + hydrationReleased);

            entity.Add(meta);
        }
    }
}
