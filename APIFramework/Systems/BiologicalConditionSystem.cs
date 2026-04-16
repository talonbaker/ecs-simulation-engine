using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Observes the current physiological state and sets or clears biological condition tags.
/// This system is purely observational — it reads resources and writes tags.
/// It does NOT spawn food, water, or any other entities. That is the job of
/// FeedingSystem and DrinkingSystem, which act after BrainSystem has decided
/// what is currently the dominant drive.
///
/// Pipeline position: 2 of 8 — after MetabolismSystem drains resources,
/// before BrainSystem scores drives.
/// </summary>
public class BiologicalConditionSystem : ISystem
{
    private readonly BiologicalConditionSystemConfig _cfg;

    public BiologicalConditionSystem(BiologicalConditionSystemConfig cfg) => _cfg = cfg;

    public void Update(EntityManager em, float deltaTime)
    {
        if (deltaTime <= 0) return;

        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            var meta = entity.Get<MetabolismComponent>();

            // Hunger and Thirst are computed: Hunger = 100 - Satiation, Thirst = 100 - Hydration
            ToggleTag<ThirstTag>   (entity, meta.Thirst >= _cfg.ThirstTagThreshold);
            ToggleTag<DehydratedTag>(entity, meta.Thirst >= _cfg.DehydratedTagThreshold);
            ToggleTag<HungerTag>   (entity, meta.Hunger >= _cfg.HungerTagThreshold);
            ToggleTag<StarvingTag> (entity, meta.Hunger >= _cfg.StarvingTagThreshold);
            ToggleTag<IrritableTag>(entity,
                meta.Hunger > _cfg.IrritableThreshold ||
                meta.Thirst > _cfg.IrritableThreshold);
        }
    }

    private static void ToggleTag<T>(Entity entity, bool condition) where T : struct
    {
        if (condition && !entity.Has<T>())      entity.Add(new T());
        else if (!condition && entity.Has<T>()) entity.Remove<T>();
    }
}
