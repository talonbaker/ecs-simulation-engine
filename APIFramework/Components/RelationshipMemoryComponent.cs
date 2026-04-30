using System;
using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>
/// Lives on the relationship entity (sibling to RelationshipComponent).
/// Bounded ring buffer of recent memory entries, newest last.
/// Capacity is enforced by MemoryRecordingSystem, not by this struct.
/// </summary>
public struct RelationshipMemoryComponent
{
    private IReadOnlyList<MemoryEntry>? _recent;

    /// <summary>Bounded list of the most recent memory entries on this relationship, newest last.</summary>
    public IReadOnlyList<MemoryEntry> Recent
    {
        readonly get => _recent ?? Array.Empty<MemoryEntry>();
        set => _recent = value;
    }
}
