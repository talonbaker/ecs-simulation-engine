using System.Collections.Generic;
using APIFramework.Systems.Narrative;

namespace APIFramework.Components;

/// <summary>
/// One recorded memory entry on a relationship or personal memory component.
/// Mirrors Warden.Contracts.Telemetry.MemoryEventDto in shape so projection
/// is field-for-field. Id is deterministic per (Tick, Kind, ParticipantIds).
/// </summary>
public readonly record struct MemoryEntry(
    string             Id,
    long               Tick,
    NarrativeEventKind Kind,
    IReadOnlyList<int> ParticipantIds,
    string?            RoomId,
    string             Detail,
    bool               Persistent
);
