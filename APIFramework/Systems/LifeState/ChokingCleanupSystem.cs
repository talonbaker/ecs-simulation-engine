using System.Linq;
using APIFramework.Components;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Removes <see cref="IsChokingTag"/> and <see cref="ChokingComponent"/> from NPCs
/// that have transitioned to <see cref="LifeState.Deceased"/> as a result of choking.
///
/// Runs last in Cleanup phase — AFTER <see cref="LifeStateTransitionSystem"/> — so
/// the Deceased state flip has already been applied when this system checks it.
/// The NPC is dead; the choking-specific components are no longer needed.
///
/// Design note: only removes on Deceased (not on rescue/recovery). If a rescue mechanic
/// is added in a future work packet, it should remove these components via its own path
/// at rescue time, before this system would see them on a Deceased entity.
///
/// WP-3.0.1: Choking-on-Food Scenario.
///
/// Phase: <see cref="SystemPhase.Cleanup"/>. Must run AFTER <see cref="LifeStateTransitionSystem"/>
/// in the same phase so it can observe the just-flipped Deceased state on the NPC.
/// Reads: <see cref="LifeStateComponent"/>, <see cref="IsChokingTag"/>.
/// Writes: removes <see cref="IsChokingTag"/> and <see cref="ChokingComponent"/> from the NPC.
/// Does not write <see cref="LifeStateComponent"/> — that is owned exclusively by
/// <see cref="LifeStateTransitionSystem"/>.
/// </remarks>
/// <seealso cref="ChokingDetectionSystem"/>
/// <seealso cref="LifeStateTransitionSystem"/>
public sealed class ChokingCleanupSystem : ISystem
{
    /// <summary>
    /// Removes choking markers from any NPC that has just transitioned to Deceased.
    /// </summary>
    /// <param name="em">Entity manager used to query NPCs with <see cref="IsChokingTag"/>.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused; cleanup is event-state driven, not time-driven).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<IsChokingTag>().ToList())
        {
            if (!entity.Has<LifeStateComponent>()) continue;
            if (entity.Get<LifeStateComponent>().State != LifeState.Deceased) continue;

            entity.Remove<IsChokingTag>();

            if (entity.Has<ChokingComponent>())
                entity.Remove<ChokingComponent>();
        }
    }
}
