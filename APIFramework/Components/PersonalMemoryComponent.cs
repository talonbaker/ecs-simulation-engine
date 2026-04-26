using System;
using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>
/// Lives on each NPC entity. Receives solo memories (one participant) and
/// fan-out memories from 3+ participant candidates.
/// Capacity is enforced by MemoryRecordingSystem, not by this struct.
/// </summary>
public struct PersonalMemoryComponent
{
    private IReadOnlyList<MemoryEntry>? _recent;

    public IReadOnlyList<MemoryEntry> Recent
    {
        readonly get => _recent ?? Array.Empty<MemoryEntry>();
        set => _recent = value;
    }
}
