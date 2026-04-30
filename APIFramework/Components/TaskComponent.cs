using System;

namespace APIFramework.Components;

/// <summary>
/// Represents a discrete work item assigned to one NPC.
/// Lives on a task entity (tagged with <see cref="TaskTag"/>), not on the NPC.
/// DeadlineTick and CreatedTick are game-seconds from <see cref="Core.SimulationClock.TotalTime"/>.
/// </summary>
public struct TaskComponent
{
    /// <summary>Total effort required to complete the task, in game-hours.</summary>
    public float EffortHours;       // total effort required, in game-hours
    /// <summary>Game-second at which the task becomes overdue (gains <c>OverdueTag</c>).</summary>
    public long  DeadlineTick;      // game-second at which the task becomes overdue
    /// <summary>Task priority in [0, 100]; higher = more urgent.</summary>
    public int   Priority;          // 0..100; higher = more urgent
    /// <summary>Fraction of the task completed, in [0, 1].</summary>
    public float Progress;          // 0..1; fraction complete
    /// <summary>Quality of the work-in-progress, in [0, 1]. Degrades under stress / poor physiology.</summary>
    public float QualityLevel;      // 0..1; degrades under stress / poor physiology
    /// <summary>Entity id of the NPC assigned to this task; <see cref="Guid.Empty"/> if unassigned.</summary>
    public Guid  AssignedNpcId;     // Guid.Empty = unassigned
    /// <summary>Game-second at which the task was created.</summary>
    public long  CreatedTick;       // game-second at creation
}
