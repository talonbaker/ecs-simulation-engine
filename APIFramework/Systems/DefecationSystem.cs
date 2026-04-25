using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Acts when Defecate is the dominant drive — empties the colon.
///
/// PIPELINE POSITION
/// ─────────────────
///   Behavior (40) — after BrainSystem scores drives, before Elimination systems fill the colon.
///
/// MECHANICS
/// ─────────
/// When Dominant == Defecate, the entity finds a "bathroom" (abstracted away entirely
/// at this simulation scale) and empties the colon. StoolVolumeMl resets to 0.
/// DefecationUrgeTag and BowelCriticalTag are cleared by ColonSystem next tick once
/// the volume drops below their thresholds.
///
/// FUTURE WORK
/// ───────────
/// - Add a DefecationDuration so emptying takes game-time instead of being instant.
/// - Require a bathroom entity in range before allowing defecation.
/// - Apply a small Joy/Relief spike to MoodComponent after a successful defecation.
/// </summary>
public class DefecationSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<DriveComponent>().ToList())
        {
            var drives = entity.Get<DriveComponent>();
            if (drives.Dominant != DesireType.Defecate) continue;
            if (!entity.Has<ColonComponent>()) continue;

            var colon = entity.Get<ColonComponent>();
            if (colon.IsEmpty) continue;

            // Empty the colon — defecation complete.
            colon.StoolVolumeMl = 0f;
            entity.Add(colon);
        }
    }
}
