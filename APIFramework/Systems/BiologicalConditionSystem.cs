using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Audio;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Condition phase. Observes physiology values (Hunger, Thirst) on
/// <see cref="MetabolismComponent"/> and toggles sensation tags accordingly.
/// </summary>
/// <remarks>
/// Reads: <see cref="MetabolismComponent"/>, <see cref="LifeStateComponent"/>.<br/>
/// Writes: hunger/thirst/irritable tags listed above (single writer of these tags).<br/>
/// Phase: Condition. Runs after <see cref="MetabolismSystem"/> drains resources and
/// before <see cref="BrainSystem"/> scores drives.
/// </remarks>
public class BiologicalConditionSystem : ISystem
{
    private readonly BiologicalConditionSystemConfig _cfg;
    private readonly SoundTriggerBus?                _soundBus;
    private readonly SimulationClock?                _clock;

    /// <summary>Constructs the system with its tag-threshold tuning.</summary>
    public BiologicalConditionSystem(BiologicalConditionSystemConfig cfg,
        SoundTriggerBus? soundBus = null, SimulationClock? clock = null)
    {
        _cfg      = cfg;
        _soundBus = soundBus;
        _clock    = clock;
    }

    /// <summary>Per-tick observation pass that toggles biological-condition tags.</summary>
    public void Update(EntityManager em, float deltaTime)
    {
        if (deltaTime <= 0) return;

        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            if (!LifeStateGuard.IsBiologicallyTicking(entity)) continue;

            var meta = entity.Get<MetabolismComponent>();

            bool newThirst     = ToggleTagWithTransition<ThirstTag>   (entity, meta.Thirst >= _cfg.ThirstTagThreshold);
            bool newDehydrated = ToggleTagWithTransition<DehydratedTag>(entity, meta.Thirst >= _cfg.DehydratedTagThreshold);
            bool newHunger     = ToggleTagWithTransition<HungerTag>   (entity, meta.Hunger >= _cfg.HungerTagThreshold);
            ToggleTagWithTransition<StarvingTag>(entity, meta.Hunger >= _cfg.StarvingTagThreshold);
            bool newIrritable  = ToggleTagWithTransition<IrritableTag>(entity,
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

    private static bool ToggleTagWithTransition<T>(Entity entity, bool condition) where T : struct
    {
        if (condition && !entity.Has<T>()) { entity.Add(new T()); return true; }
        if (!condition && entity.Has<T>()) entity.Remove<T>();
        return false;
    }
}
