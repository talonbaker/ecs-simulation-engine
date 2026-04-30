using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Attaches <see cref="LifeStateComponent"/> to every NPC that has
/// <see cref="NpcArchetypeComponent"/> but no life state yet.
/// Phase: PreUpdate — runs before any system that reads life state.
/// Idempotent: entities that already have <see cref="LifeStateComponent"/> are skipped.
///
/// Every NPC spawned by the cast generator gains LifeStateComponent at boot with State == Alive.
/// </summary>
/// <remarks>
/// Phase: <see cref="SystemPhase.PreUpdate"/>. Must run before any system that reads
/// <see cref="LifeStateComponent"/> (in particular, before <see cref="LifeStateGuard.IsAlive"/>
/// gates in cognition and physiology). This is an initializer, NOT the single writer of
/// <see cref="LifeStateComponent"/> — once an NPC has the component, only
/// <see cref="LifeStateTransitionSystem"/> may modify it.
/// </remarks>
/// <seealso cref="LifeStateTransitionSystem"/>
/// <seealso cref="LifeStateGuard"/>
public class LifeStateInitializerSystem : ISystem
{
    /// <summary>
    /// Attaches a fresh <see cref="LifeStateComponent"/> (State = Alive) to any NPC archetype entity that lacks one.
    /// </summary>
    /// <param name="em">Entity manager used to query NPCs.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused; this is a one-shot-per-entity initializer).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<NpcTag>().ToList())
        {
            if (!entity.Has<NpcArchetypeComponent>()) continue;
            if (entity.Has<LifeStateComponent>()) continue;

            entity.Add(new LifeStateComponent
            {
                State = Components.LifeState.Alive,
                LastTransitionTick = 0,
                IncapacitatedTickBudget = 0,
                PendingDeathCause = CauseOfDeath.Unknown
            });
        }
    }
}
