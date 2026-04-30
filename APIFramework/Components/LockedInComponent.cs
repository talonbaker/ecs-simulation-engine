namespace APIFramework.Components;

/// <summary>
/// Marks an NPC as trapped in a room with no reachable exit and insufficient food/water.
/// Attached when LockoutDetectionSystem detects both conditions are true.
/// Removed when the NPC regains exit reachability OR transitions to Deceased.
///
/// The starvation countdown decrements once per game-day, not per tick.
/// So StarvationTickBudget is "game-days the NPC can survive without food," not ticks.
/// </summary>
public struct LockedInComponent
{
    /// <summary>Game tick at which lockout was first detected.</summary>
    public long FirstDetectedTick;

    /// <summary>
    /// Game-days remaining before starvation death. Decrements once per game-day
    /// when LockoutDetectionSystem runs (not every tick). When this reaches 0,
    /// the NPC transitions to Deceased(StarvedAlone).
    /// </summary>
    public int StarvationTickBudget;
}
