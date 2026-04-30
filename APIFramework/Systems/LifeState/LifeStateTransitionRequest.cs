using APIFramework.Components;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Single-tick request to transition an NPC's LifeStateComponent.
/// Enqueued by producers (e.g. choking scenario in WP-3.0.1);
/// drained by LifeStateTransitionSystem at Cleanup phase.
/// </summary>
public record LifeStateTransitionRequest(
    Guid NpcId,
    Components.LifeState TargetState,
    CauseOfDeath Cause
);
