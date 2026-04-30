using System.Linq;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Removes <see cref="IsFaintingTag"/> and <see cref="FaintingComponent"/> from NPCs
/// that have returned to <see cref="LifeState.Alive"/> after a faint.
///
/// Runs AFTER <see cref="LifeStateTransitionSystem"/> in the Cleanup phase so the
/// Alive state flip has already been applied when this system checks.
///
/// Design note: only removes tags when the NPC is back to Alive. An NPC who is still
/// Incapacitated (faint duration not yet elapsed) retains their tags. An NPC who
/// somehow became Deceased while tagged (should not happen given the +1 budget
/// design, but defensive coding applies) also retains the tags for audit purposes —
/// <see cref="ChokingCleanupSystem"/> handles Deceased-only cleanup for its own tags.
///
/// WP-3.0.6: Fainting System.
/// </summary>
/// <seealso cref="FaintingDetectionSystem"/>
/// <seealso cref="FaintingRecoverySystem"/>
public sealed class FaintingCleanupSystem : ISystem
{
    /// <summary>
    /// Per-tick entry point. Strips fainting tags and components from any NPC that has
    /// returned to <see cref="LifeState.Alive"/>.
    /// </summary>
    /// <param name="em">Entity manager — queried for entities tagged <c>IsFaintingTag</c>.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<IsFaintingTag>().ToList())
        {
            if (!entity.Has<LifeStateComponent>()) continue;
            if (entity.Get<LifeStateComponent>().State != LifeState.Alive) continue;

            entity.Remove<IsFaintingTag>();

            if (entity.Has<FaintingComponent>())
                entity.Remove<FaintingComponent>();
        }
    }
}
