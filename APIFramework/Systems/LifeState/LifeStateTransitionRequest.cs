using System;
using APIFramework.Components;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// An enqueued request to change an NPC's <see cref="LifeState"/>.
/// Producers (choking system, slip-and-fall system, etc.) push requests via
/// <see cref="LifeStateTransitionSystem.RequestTransition"/>.
/// The transition system drains the queue each tick in deterministic order.
/// </summary>
internal sealed record LifeStateTransitionRequest(
    Guid          NpcId,
    LifeState     TargetState,
    CauseOfDeath  Cause,
    /// <summary>
    /// Per-cause override for the incapacitation tick budget.
    /// When null, <see cref="LifeStateConfig.DefaultIncapacitatedTicks"/> is used.
    /// WP-3.0.1+: choking passes ChokingConfig.IncapacitationTicks here.
    /// </summary>
    int?          IncapacitationTicksOverride = null);
