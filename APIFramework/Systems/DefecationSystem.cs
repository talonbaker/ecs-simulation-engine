using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Behavior phase. Acts when <see cref="DesireType.Defecate"/> is the dominant drive —
/// empties the colon by zeroing <see cref="ColonComponent"/>.StoolVolumeMl. Skipped
/// when blocked by a publicEmotion <see cref="BlockedActionsComponent"/> entry.
/// </summary>
/// <remarks>
/// Reads: <see cref="DriveComponent"/>, <see cref="ColonComponent"/>,
/// <see cref="BlockedActionsComponent"/>, <see cref="LifeStateComponent"/>.<br/>
/// Writes: <see cref="ColonComponent"/>.StoolVolumeMl (zeroes it on action).<br/>
/// Phase: Behavior, after <see cref="BrainSystem"/> scores drives and before
/// <see cref="LargeIntestineSystem"/> refills the colon.
/// </remarks>
public class DefecationSystem : ISystem
{
    /// <summary>Per-tick action pass; empties the colon for any NPC whose dominant drive is Defecate.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds, unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<DriveComponent>().ToList())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip Incapacitated/Deceased NPCs (autonomous trigger only)

            var drives = entity.Get<DriveComponent>();
            if (drives.Dominant != DesireType.Defecate) continue;
            if (!entity.Has<ColonComponent>()) continue;

            // Social inhibition veto: publicEmotion overrides bowel urgency.
            if (entity.Has<BlockedActionsComponent>() &&
                entity.Get<BlockedActionsComponent>().Contains(BlockedActionClass.Defecate))
                continue;

            var colon = entity.Get<ColonComponent>();
            if (colon.IsEmpty) continue;

            // Empty the colon — defecation complete.
            colon.StoolVolumeMl = 0f;
            entity.Add(colon);
        }
    }
}
