using System;
using APIFramework.Components;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// An enqueued request to change an NPC's <see cref="LifeState"/>.
/// Producers (choking system, slip-and-fall system, etc.) push requests via
/// <see cref="LifeStateTransitionSystem.RequestTransition"/>.
/// The transition system drains the queue each tick in deterministic order.
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
