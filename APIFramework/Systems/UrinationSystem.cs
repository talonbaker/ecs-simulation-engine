using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Empties the bladder when Pee is the dominant drive.
///
/// PIPELINE POSITION
/// ─────────────────
///   Phase: Behavior (40) — alongside FeedingSystem, DrinkingSystem, SleepSystem,
///   DefecationSystem. All action systems are gated on DriveComponent.Dominant.
///
/// CONTRACT
/// ─────────
/// When Dominant == Pee:
///   • Sets BladderComponent.VolumeML to 0.
///   • BladderSystem (next Elimination tick) will clear UrinationUrgeTag and
///     BladderCriticalTag automatically when it sees the empty volume.
///
/// Backwards-compatible: entities without BladderComponent are silently skipped.
/// </summary>
public class UrinationSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<DriveComponent>().ToList())
        {
            var drives = entity.Get<DriveComponent>();
            if (drives.Dominant != DesireType.Pee) continue;
            if (!entity.Has<BladderComponent>()) continue;

            // Social inhibition veto: publicEmotion overrides bladder urgency.
            if (entity.Has<BlockedActionsComponent>() &&
                entity.Get<BlockedActionsComponent>().Contains(BlockedActionClass.Urinate))
                continue;

            var bladder     = entity.Get<BladderComponent>();
            bladder.VolumeML = 0f;
            entity.Add(bladder);
        }
    }
}
