using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Cleans up choking state when an NPC transitions to Deceased.
///
/// Runs at the very end of the Cleanup phase (after LifeStateTransitionSystem).
/// Iterates NPCs with IsChokingTag and removes the tag and ChokingComponent
/// if the NPC has just transitioned to Deceased.
///
/// The deceased NPC no longer "is choking" — they have died of choking,
/// which is recorded by CauseOfDeathComponent.
/// </summary>
public class ChokingCleanupSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        // Iterate all choking NPCs and check for Deceased state
        var chokingNpcs = em.Query<IsChokingTag>().ToList();

        foreach (var npc in chokingNpcs)
        {
            if (!npc.Has<LifeStateComponent>()) continue;

            var state = npc.Get<LifeStateComponent>().State;
            if (state == Components.LifeState.Deceased)
            {
                // Remove the choking markers — the deceased no longer "is choking"
                npc.Remove<IsChokingTag>();
                npc.Remove<ChokingComponent>();
            }
        }
    }
}
