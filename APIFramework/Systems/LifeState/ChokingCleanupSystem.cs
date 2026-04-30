using APIFramework.Components;
using APIFramework.Core;

using LS = global::APIFramework.Components.LifeState;

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
/// <remarks>
/// Phase: <see cref="SystemPhase.Cleanup"/>. Must run AFTER <see cref="LifeStateTransitionSystem"/>
/// in the same phase so it can observe the just-flipped Deceased state on the NPC.
/// Reads: <see cref="LifeStateComponent"/>, <see cref="IsChokingTag"/>.
/// Writes: removes <see cref="IsChokingTag"/> and <see cref="ChokingComponent"/> from the NPC.
/// Does not write <see cref="LifeStateComponent"/> — that is owned exclusively by
/// <see cref="LifeStateTransitionSystem"/>.
/// </remarks>
/// <seealso cref="ChokingDetectionSystem"/>
/// <seealso cref="LifeStateTransitionSystem"/>
public class ChokingCleanupSystem : ISystem
{
    /// <summary>
    /// Removes choking markers from any NPC that has just transitioned to Deceased.
    /// </summary>
    /// <param name="em">Entity manager used to query NPCs with <see cref="IsChokingTag"/>.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused; cleanup is event-state driven, not time-driven).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        // Iterate all choking NPCs and check for Deceased state
        var chokingNpcs = em.Query<IsChokingTag>().ToList();

        foreach (var npc in chokingNpcs)
        {
            if (!npc.Has<LifeStateComponent>()) continue;

            var state = npc.Get<LifeStateComponent>().State;
            if (state == LS.Deceased)
            {
                // Remove the choking markers — the deceased no longer "is choking"
                npc.Remove<IsChokingTag>();
                npc.Remove<ChokingComponent>();
            }
        }
    }
}
