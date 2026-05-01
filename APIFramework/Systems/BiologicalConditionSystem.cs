using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Audio;

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
    private readonly SoundTriggerBus? _soundBus;

    public BiologicalConditionSystem(BiologicalConditionSystemConfig cfg, SoundTriggerBus? soundBus = null)
    {
        _cfg      = cfg;
        _soundBus = soundBus;
    }

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

            // Emit biological sounds based on physiological state
            if (_soundBus != null)
            {
                var pos = entity.Has<PositionComponent>() ? entity.Get<PositionComponent>() : default;

                // Yawn when exhausted (high sleepiness / low energy)
                if (entity.Has<ExhaustedTag>())
                    _soundBus.Emit(SoundTriggerKind.Yawn, entity.Id, pos.X, pos.Z, 0.4f, 0L);

                // Sigh when sad or grieving (emotional distress)
                if (entity.Has<SadTag>() || entity.Has<GriefTag>())
                    _soundBus.Emit(SoundTriggerKind.Sigh, entity.Id, pos.X, pos.Z, 0.3f, 0L);

                // Sneeze when severely dehydrated (physiological stress)
                if (entity.Has<DehydratedTag>())
                    _soundBus.Emit(SoundTriggerKind.Sneeze, entity.Id, pos.X, pos.Z, 0.7f, 0L);
            }
        }
    }

    private static void ToggleTag<T>(Entity entity, bool condition) where T : struct
    {
        if (condition && !entity.Has<T>())      entity.Add(new T());
        else if (!condition && entity.Has<T>()) entity.Remove<T>();
    }
}
