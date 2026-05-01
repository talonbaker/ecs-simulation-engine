namespace APIFramework.Components;

/// <summary>
/// Cortisol-like stress accumulator. Slow to build, slow to dissipate.
/// AcuteLevel tracks today's accumulated stress; ChronicLevel is a 7-day rolling mean.
/// BurnoutLastAppliedDay tracks when BurningOutTag was last applied, enabling the sticky cooldown.
/// </summary>
public struct StressComponent
{
    /// <summary>Today's accumulated acute stress, 0–100.</summary>
    public int    AcuteLevel;                // 0..100; today's accumulated stress
    /// <summary>Rolling 7-day mean of <see cref="AcuteLevel"/>, 0–100.</summary>
    public double ChronicLevel;              // 0..100; rolling 7-day average of AcuteLevel
    /// <summary>SimulationClock.DayNumber on which the chronic mean was last updated.</summary>
    public int    LastDayUpdated;            // SimulationClock.DayNumber at last chronic update
    /// <summary>Count of suppression-source stress events accumulated this day.</summary>
    public int    SuppressionEventsToday;
    /// <summary>Count of drive-spike-source stress events accumulated this day.</summary>
    public int    DriveSpikeEventsToday;
    /// <summary>Count of social-conflict-source stress events accumulated this day.</summary>
    public int    SocialConflictEventsToday;
    /// <summary>Count of overdue-task-source stress events accumulated this day.</summary>
    public int    OverdueTaskEventsToday;    // count of overdue-task source hits this day
    /// <summary>SimulationClock.DayNumber when <c>BurningOutTag</c> was last applied (0 = never). Drives the sticky cooldown.</summary>
    public int    BurnoutLastAppliedDay;     // DayNumber when BurningOutTag was last applied (0 = never)

    // ── Bereavement counters (WP-3.0.2) ─────────────────────────────────────
    /// <summary>
    /// Set to 1 by <see cref="APIFramework.Systems.LifeState.BereavementSystem"/> when this NPC
    /// directly witnessed a death event (was the second participant in a death narrative candidate).
    /// <see cref="APIFramework.Systems.StressSystem"/> applies
    /// <see cref="APIFramework.Config.BereavementConfig.WitnessedDeathStressGain"/> and clears to 0
    /// in the same Cleanup-phase sweep (one-shot, not per-tick accumulation).
    /// </summary>
    public int    WitnessedDeathEventsToday;

    /// <summary>
    /// Set by <see cref="APIFramework.Systems.LifeState.BereavementSystem"/> for non-witness
    /// colleagues with a relationship above the bereavement-intensity threshold.
    /// <see cref="APIFramework.Systems.StressSystem"/> applies
    /// <see cref="APIFramework.Config.BereavementConfig.BereavementStressGain"/> per count and
    /// clears to 0 (one-shot).
    /// </summary>
    public int    BereavementEventsToday;

    // ── Chore overrotation counter (WP-3.2.3) ────────────────────────────────
    /// <summary>
    /// Incremented by <see cref="APIFramework.Systems.Chores.ChoreExecutionSystem"/> when this NPC
    /// completes the same chore beyond the overrotation threshold within the rolling game-day window.
    /// <see cref="APIFramework.Systems.StressSystem"/> applies
    /// <see cref="APIFramework.Config.ChoreConfig.ChoreOverrotationStressGain"/> per count and
    /// resets to 0 on day rollover.
    /// </summary>
    public int    ChoreOverrotationEventsToday;
}
