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
/// </summary>
public sealed class ChokingCleanupSystem : ISystem
{
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
