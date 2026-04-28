using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Monitors bladder fill level and manages urination-urge tags.
///
/// PIPELINE POSITION
/// ─────────────────
///   BladderFillSystem (Physiology/10) fills BladderComponent.
///   BladderSystem     (Elimination/55) runs here, applying tags immediately.
///   UrinationSystem   (Behavior/40) acts on those tags next tick.
///
///   Note: Because Behavior(40) precedes Elimination(55), UrinationSystem always
///   reads tags set by the PREVIOUS tick — a one-tick lag identical to ColonSystem,
///   and imperceptible at any normal TimeScale.
///
/// TAG LIFECYCLE
/// ─────────────
///   VolumeML >= UrgeThresholdMl → UrinationUrgeTag  (entity feels the urge)
///   VolumeML >= CapacityMl      → BladderCriticalTag (emergency override)
///   Both tags are cleared when volume drops below their respective thresholds.
///
/// This system owns all writes to UrinationUrgeTag and BladderCriticalTag.
/// No other system should add or remove these tags.
/// </summary>
public class BladderSystem : ISystem
{
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
