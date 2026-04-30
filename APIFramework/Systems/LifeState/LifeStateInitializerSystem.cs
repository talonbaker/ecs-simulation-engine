using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Attaches <see cref="LifeStateComponent"/> with <see cref="LifeState.Alive"/> to every NPC
/// entity that does not yet have one.
///
/// Phase: PreUpdate — runs before any system that reads life state, immediately after
/// the other initializer systems (StressInitializerSystem, MaskInitializerSystem, WorkloadInitializerSystem).
///
/// Idempotent: entities already carrying <see cref="LifeStateComponent"/> are skipped.
/// No RNG is used; the operation is deterministic and order-independent.
/// </summary>
/// <seealso cref="LifeStateTransitionSystem"/>
/// <seealso cref="LifeStateGuard"/>
public sealed class LifeStateInitializerSystem : ISystem
{
    /// <summary>
    /// Per-tick entry point. Attaches a default <c>LifeStateComponent</c> (Alive) to every
    /// NPC that does not yet have one.
    /// </summary>
    /// <param name="em">Entity manager — queried for NPCs.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<NpcTag>().ToList())
        {
            if (entity.Has<LifeStateComponent>()) continue;

            entity.Add(new LifeStateComponent
            {
                State                 = LifeState.Alive,
                LastTransitionTick    = 0,
                IncapacitatedTickBudget = 0,
                PendingDeathCause     = CauseOfDeath.Unknown,
            });
        }
    }
}
