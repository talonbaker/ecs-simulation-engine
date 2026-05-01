using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Condition phase. Observes physiology values (Hunger, Thirst) on
/// <see cref="MetabolismComponent"/> and toggles sensation tags accordingly:
/// <see cref="ThirstTag"/>, <see cref="DehydratedTag"/>, <see cref="HungerTag"/>,
/// <see cref="StarvingTag"/>, and <see cref="IrritableTag"/>. Purely observational —
/// spawns nothing.
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

    /// <summary>Constructs the system with its tag-threshold tuning.</summary>
    /// <param name="cfg">Hunger/thirst/irritable threshold values.</param>
    public BiologicalConditionSystem(BiologicalConditionSystemConfig cfg) => _cfg = cfg;

    /// <summary>Per-tick observation pass that toggles biological-condition tags.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds). No-op when zero or negative.</param>
    public void Update(EntityManager em, float deltaTime)
    {
        if (deltaTime <= 0) return;

        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            if (!LifeStateGuard.IsBiologicallyTicking(entity)) continue;  // WP-3.0.0: skip Deceased NPCs (Incapacitated still ticks)

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
