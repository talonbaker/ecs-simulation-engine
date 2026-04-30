using System;
using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>
/// Per-NPC workload tracker. Attached at spawn by <see cref="Systems.WorkloadInitializerSystem"/>.
/// ActiveTasks is the list of task-entity GUIDs assigned to this NPC.
/// CurrentLoad = ActiveTasks.Count * 100 / Capacity (recomputed each tick by WorkloadSystem).
/// </summary>
public struct WorkloadComponent
{
    /// <summary>Task entity GUIDs assigned to this NPC. Null is treated as empty.</summary>
    public IReadOnlyList<Guid> ActiveTasks;   // task entity GUIDs; null treated as empty
    /// <summary>Maximum number of simultaneous active tasks (per archetype).</summary>
    public int                 Capacity;       // max simultaneous active tasks (per archetype)
    /// <summary>Current load 0–100, computed each tick by WorkloadSystem as ActiveTasks.Count × 100 / Capacity.</summary>
    public int                 CurrentLoad;    // 0..100; ActiveTasks.Count / Capacity × 100
}
