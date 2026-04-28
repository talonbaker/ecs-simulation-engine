namespace APIFramework.Components;

/// <summary>
/// Cortisol-like stress accumulator. Slow to build, slow to dissipate.
/// AcuteLevel tracks today's accumulated stress; ChronicLevel is a 7-day rolling mean.
/// BurnoutLastAppliedDay tracks when BurningOutTag was last applied, enabling the sticky cooldown.
/// </summary>
public struct StressComponent
{
    public int    AcuteLevel;                // 0..100; today's accumulated stress
    public double ChronicLevel;              // 0..100; rolling 7-day average of AcuteLevel
    public int    LastDayUpdated;            // SimulationClock.DayNumber at last chronic update
    public int    SuppressionEventsToday;
    public int    DriveSpikeEventsToday;
    public int    SocialConflictEventsToday;
    public int    OverdueTaskEventsToday;    // count of overdue-task source hits this day
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
}
