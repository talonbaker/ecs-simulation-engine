using System;
using APIFramework.Components;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// An enqueued request to change an NPC's <see cref="LifeState"/>.
/// Producers (choking system, slip-and-fall system, etc.) push requests via
/// <see cref="LifeStateTransitionSystem.RequestTransition"/>.
/// The transition system drains the queue each tick in deterministic order.
/// </summary>
/// <param name="NpcId">Entity GUID of the NPC to transition.</param>
/// <param name="TargetState">Target <see cref="LifeState"/> after the request is applied.</param>
/// <param name="Cause">Cause of death; required for transitions to <see cref="LifeState.Deceased"/>.</param>
/// <param name="IncapacitationTicksOverride">
/// Per-cause override for the incapacitation tick budget.
/// When null, <c>LifeStateConfig.DefaultIncapacitatedTicks</c> is used.
/// WP-3.0.1+: choking passes ChokingConfig.IncapacitationTicks here.
/// </param>
internal sealed record LifeStateTransitionRequest(
    Guid          NpcId,
    LifeState     TargetState,
    CauseOfDeath  Cause,
    int?          IncapacitationTicksOverride = null);
