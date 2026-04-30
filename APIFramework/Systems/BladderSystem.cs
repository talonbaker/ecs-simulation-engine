using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Elimination phase. Monitors <see cref="BladderComponent"/> fill level and toggles
/// <see cref="UrinationUrgeTag"/> and <see cref="BladderCriticalTag"/> based on the
/// component's HasUrge / IsCritical thresholds.
/// </summary>
/// <remarks>
/// Reads: <see cref="BladderComponent"/>, <see cref="LifeStateComponent"/>.<br/>
/// Writes: <see cref="UrinationUrgeTag"/> and <see cref="BladderCriticalTag"/>
/// (single writer of both).<br/>
/// Phase: Elimination, after <see cref="BladderFillSystem"/> (Physiology). Because
/// Behavior(40) precedes Elimination(55), <see cref="UrinationSystem"/> reads tags
/// from the previous tick — a one-tick lag imperceptible at normal TimeScale.
/// </remarks>
/// <seealso cref="BladderFillSystem"/>
/// <seealso cref="UrinationSystem"/>
public class BladderSystem : ISystem
{
    /// <summary>Per-tick tag-toggle pass over <see cref="BladderComponent"/> entities.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds, unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<BladderComponent>().ToList())
        {
            if (!LifeStateGuard.IsBiologicallyTicking(entity)) continue;  // WP-3.0.0: skip Deceased NPCs (Incapacitated still ticks)

            var bladder = entity.Get<BladderComponent>();

            ToggleTag<UrinationUrgeTag> (entity, bladder.HasUrge);
            ToggleTag<BladderCriticalTag>(entity, bladder.IsCritical);
        }
    }

    private static void ToggleTag<T>(Entity entity, bool condition) where T : struct
    {
        if (condition  && !entity.Has<T>()) entity.Add(new T());
        else if (!condition && entity.Has<T>()) entity.Remove<T>();
    }
}
