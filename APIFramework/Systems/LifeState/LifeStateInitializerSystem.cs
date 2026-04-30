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
public class LifeStateInitializerSystem : ISystem
{
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
