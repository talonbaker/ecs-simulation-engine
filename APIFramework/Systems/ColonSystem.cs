using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Monitors colon fill level and manages defecation-urge tags.
///
/// PIPELINE POSITION
/// ─────────────────
///   LargeIntestineSystem (Elimination/55) fills ColonComponent.
///   ColonSystem (Elimination/55) runs immediately after, applying tags.
///   DefecationSystem (Behavior/40) acts on those tags next tick.
///
///   Note: Because Behavior(40) precedes Elimination(55), DefecationSystem always
///   reads tags set by the PREVIOUS tick — a one-tick lag that is imperceptible at
///   any normal TimeScale.
///
/// TAG LIFECYCLE
/// ─────────────
///   StoolVolumeMl >= UrgeThresholdMl  → DefecationUrgeTag (entity feels the urge)
///   StoolVolumeMl >= CapacityMl       → BowelCriticalTag  (emergency override)
///   Both tags are cleared when volume drops below their respective thresholds.
///
/// This system owns all writes to DefecationUrgeTag and BowelCriticalTag.
/// No other system should add or remove these tags.
/// </summary>
public class ColonSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<ColonComponent>().ToList())
        {
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
