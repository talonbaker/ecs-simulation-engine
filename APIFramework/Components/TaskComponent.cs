using System;

namespace APIFramework.Components;

/// <summary>
/// Represents a discrete work item assigned to one NPC.
/// Lives on a task entity (tagged with <see cref="TaskTag"/>), not on the NPC.
/// DeadlineTick and CreatedTick are game-seconds from <see cref="Core.SimulationClock.TotalTime"/>.
/// </summary>
public struct TaskComponent
{
    public float EffortHours;       // total effort required, in game-hours
    public long  DeadlineTick;      // game-second at which the task becomes overdue
    public int   Priority;          // 0..100; higher = more urgent
    public float Progress;          // 0..1; fraction complete
    public float QualityLevel;      // 0..1; degrades under stress / poor physiology
    public Guid  AssignedNpcId;     // Guid.Empty = unassigned
    public long  CreatedTick;       // game-second at creation
}
