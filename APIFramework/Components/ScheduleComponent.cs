using System;
using System.Collections.Generic;

namespace APIFramework.Components;

public enum ScheduleActivityKind
{
    AtDesk,
    Break,
    Meeting,
    Lunch,
    Outside,
    Roaming,
    Sleeping
}

/// <summary>
/// One block in an NPC's daily routine. Hours are floats on SimulationClock.GameHour (0..24).
/// When EndHour &lt; StartHour the block wraps past midnight (e.g. 17:00 → 06:00).
/// Blocks within a schedule must cover the full 24h day with no gaps and no overlaps.
/// </summary>
public readonly record struct ScheduleBlock(
    float StartHour,
    float EndHour,
    string AnchorId,
    ScheduleActivityKind Activity);

/// <summary>Per-NPC routine. Populated once at spawn by ScheduleSpawnerSystem; never mutated.</summary>
public struct ScheduleComponent
{
    public IReadOnlyList<ScheduleBlock> Blocks;
}

/// <summary>
/// Updated each tick by ScheduleSystem from ScheduleComponent + SimulationClock.
/// Read by ActionSelectionSystem during candidate enumeration.
/// </summary>
public struct CurrentScheduleBlockComponent
{
    public int                  ActiveBlockIndex;  // -1 if no active block
    public Guid                 AnchorEntityId;    // Guid.Empty if none
    public ScheduleActivityKind Activity;
}
