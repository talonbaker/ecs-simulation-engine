using APIFramework.Components;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Single-tick request to transition an NPC's LifeStateComponent.
/// Enqueued by producers (e.g. choking scenario in WP-3.0.1);
/// drained by LifeStateTransitionSystem at Cleanup phase.
/// </summary>
/// <param name="NpcId">Guid of the NPC to transition.</param>
/// <param name="TargetState">Desired new <see cref="Components.LifeState"/> (Alive, Incapacitated, or Deceased).</param>
/// <param name="Cause">Cause of death recorded with the transition; relevant only when transitioning toward Incapacitated or Deceased. Use <see cref="CauseOfDeath.Unknown"/> when not applicable.</param>
/// <seealso cref="LifeStateTransitionSystem"/>
public record LifeStateTransitionRequest(
    Guid NpcId,
    Components.LifeState TargetState,
    CauseOfDeath Cause
);
