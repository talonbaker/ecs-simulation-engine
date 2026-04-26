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
    public int    BurnoutLastAppliedDay;     // DayNumber when BurningOutTag was last applied (0 = never)
}
