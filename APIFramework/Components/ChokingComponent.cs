using APIFramework.Components;

namespace APIFramework.Components;

/// <summary>
/// Attached to an NPC entity for the duration of a choking episode.
/// Added by <see cref="APIFramework.Systems.LifeState.ChokingDetectionSystem"/> at the moment of choke.
/// Removed by <see cref="APIFramework.Systems.LifeState.ChokingCleanupSystem"/> when the NPC dies.
///
/// The canonical countdown lives in <see cref="LifeStateComponent.IncapacitatedTickBudget"/>;
/// this component mirrors it for convenient querying by other systems (e.g. a future
/// rescue-mechanic system) without having to cross-reference both components.
///
/// WP-3.0.1: Choking-on-Food Scenario.
/// </summary>
public struct ChokingComponent
{
    /// <summary>SimulationClock.CurrentTick at the moment the choke was triggered.</summary>
    public long ChokeStartTick;

    /// <summary>Ticks remaining before the NPC dies (mirrors LifeStateComponent.IncapacitatedTickBudget).</summary>
    public int RemainingTicks;

    /// <summary>
    /// Toughness (0..1) of the bolus that triggered the choke.
    /// Stored for telemetry and scenario tuning (completion-note tables, archetype follow-up).
    /// </summary>
    public float BolusSize;

    /// <summary>
    /// Cause that will be registered when death occurs. Always <see cref="CauseOfDeath.Choked"/> at v0.1.
    /// Reserved for future scenario types that use a similar component (e.g. foreign-body aspiration).
    /// </summary>
    public CauseOfDeath PendingCause;

    /// <summary>
    /// Returns a human-readable summary of the choking state for telemetry and logging.
    /// </summary>
    /// <returns>A formatted string showing start tick, remaining ticks, and bolus volume.</returns>
    public override string ToString() =>
        $"Choking (started: {ChokeStartTick}, remaining: {RemainingTicks}s, bolus: {BolusSize:F3}ml)";
}
