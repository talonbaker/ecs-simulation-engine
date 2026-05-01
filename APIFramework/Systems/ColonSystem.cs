using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Elimination phase. Monitors <see cref="ColonComponent"/> fill level and toggles
/// <see cref="DefecationUrgeTag"/> and <see cref="BowelCriticalTag"/> based on the
/// component's HasUrge / IsCritical thresholds.
/// </summary>
/// <remarks>
/// Reads: <see cref="ColonComponent"/>, <see cref="LifeStateComponent"/>.<br/>
/// Writes: <see cref="DefecationUrgeTag"/> and <see cref="BowelCriticalTag"/>
/// (single writer of both).<br/>
/// Phase: Elimination, after <see cref="LargeIntestineSystem"/> fills the colon.
/// Because Behavior(40) precedes Elimination(55), <see cref="DefecationSystem"/>
/// reads tags from the previous tick — a one-tick lag imperceptible at normal TimeScale.
/// </remarks>
/// <seealso cref="LargeIntestineSystem"/>
/// <seealso cref="DefecationSystem"/>
public class ColonSystem : ISystem
{
    /// <summary>Per-tick tag-toggle pass over <see cref="ColonComponent"/> entities.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds, unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<ColonComponent>().ToList())
        {
            if (!LifeStateGuard.IsBiologicallyTicking(entity)) continue;  // WP-3.0.0: skip Deceased NPCs (Incapacitated still ticks)

            var colon = entity.Get<ColonComponent>();

            ToggleTag<DefecationUrgeTag>(entity, colon.HasUrge);
            ToggleTag<BowelCriticalTag> (entity, colon.IsCritical);
        }
    }

    private static void ToggleTag<T>(Entity entity, bool condition) where T : struct
    {
        if (condition  && !entity.Has<T>()) entity.Add(new T());
        else if (!condition && entity.Has<T>()) entity.Remove<T>();
    }
}
