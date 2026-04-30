using System;
using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>The activity an NPC is scheduled to perform during a <see cref="ScheduleBlock"/>.</summary>
public enum ScheduleActivityKind
{
    /// <summary>NPC is working at their assigned desk.</summary>
    AtDesk,
    /// <summary>NPC is on a short break (e.g. coffee, restroom).</summary>
    Break,
    /// <summary>NPC is in a meeting.</summary>
    Meeting,
    /// <summary>NPC is at lunch.</summary>
    Lunch,
    /// <summary>NPC is outside the building.</summary>
    Outside,
    /// <summary>NPC is unscheduled and roaming.</summary>
    Roaming,
    /// <summary>NPC is sleeping (off-shift / overnight).</summary>
    Sleeping
}

/// <summary>
/// One block in an NPC's daily routine. Hours are floats on SimulationClock.GameHour (0..24).
/// When EndHour &lt; StartHour the block wraps past midnight (e.g. 17:00 → 06:00).
/// Blocks within a schedule must cover the full 24h day with no gaps and no overlaps.
/// </summary>
/// <param name="StartHour">Block start hour as a float in [0, 24).</param>
/// <param name="EndHour">Block end hour as a float in [0, 24); may be less than StartHour to wrap past midnight.</param>
/// <param name="AnchorId">Authored anchor tag the NPC should be at during this block.</param>
/// <param name="Activity">What the NPC is doing during this block.</param>
public readonly record struct ScheduleBlock(
    float StartHour,
    float EndHour,
    string AnchorId,
    ScheduleActivityKind Activity);

/// <summary>Per-NPC routine. Populated once at spawn by ScheduleSpawnerSystem; never mutated.</summary>
public struct ScheduleComponent
{
    /// <summary>Ordered list of schedule blocks covering the full 24-hour day.</summary>
    public IReadOnlyList<ScheduleBlock> Blocks;
}

/// <summary>
/// Updated each tick by ScheduleSystem from ScheduleComponent + SimulationClock.
/// Read by ActionSelectionSystem during candidate enumeration.
/// </summary>
public struct CurrentScheduleBlockComponent
{
    /// <summary>Index into ScheduleComponent.Blocks of the currently active block; -1 if none.</summary>
    public int                  ActiveBlockIndex;  // -1 if no active block
    /// <summary>Resolved anchor entity for the active block; <see cref="Guid.Empty"/> if none.</summary>
    public Guid                 AnchorEntityId;    // Guid.Empty if none
    /// <summary>Activity from the active block (mirrored here for fast read).</summary>
    public ScheduleActivityKind Activity;
}
