using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

public class BiologicalConditionSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        // Snapshot to prevent 'Collection Modified' errors if we add/remove tags
        var entities = em.Query<MetabolismComponent>().ToList();

        foreach (var entity in entities)
        {
            var meta = entity.Get<MetabolismComponent>();

            // --- Hunger Logic (75% Threshold) ---
            if (meta.Hunger >= 75f && !entity.Has<HungerTag>())
                entity.Add(new HungerTag());
            else if (meta.Hunger < 75f && entity.Has<HungerTag>())
                entity.Remove<HungerTag>();

            // --- Thirst Logic (70% Threshold) ---
            if (meta.Thirst >= 70f && !entity.Has<ThirstTag>())
                entity.Add(new ThirstTag());
            else if (meta.Thirst < 70f && entity.Has<ThirstTag>())
                entity.Remove<ThirstTag>();

            // --- Extreme States ---
            if (meta.Hunger >= 110f && !entity.Has<StarvingTag>())
                entity.Add(new StarvingTag());
        }
    }
}