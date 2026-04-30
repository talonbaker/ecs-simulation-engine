using System.Collections.Generic;

namespace APIFramework.Systems.Chronicle;

/// <summary>
/// Engine-internal record for a single persistent chronicle event.
/// Produced by <see cref="PersistenceThresholdDetector"/> and stored in <see cref="ChronicleService"/>.
/// </summary>
/// <param name="Id">RFC 4122 v4 UUID generated from <see cref="APIFramework.Core.SeededRandom"/>.</param>
/// <param name="Kind">High-level chronicle classification (mirrors Warden.Contracts).</param>
/// <param name="Tick">Simulation tick on which the originating candidate was emitted.</param>
/// <param name="ParticipantIds">EntityIntId list copied from the originating candidate.</param>
/// <param name="Location">Room id of the candidate, or empty when not room-bound.</param>
/// <param name="Description">Human-readable summary, capped at 280 characters.</param>
/// <param name="Persistent">Always true — non-persistent events are not promoted to chronicle entries.</param>
/// <param name="PhysicalManifestEntityId">GUID-string of any spawned Stain or BrokenItem entity, or null when no physical manifestation was created.</param>
public sealed record ChronicleEntry(
    string                 Id,
    ChronicleEventKind     Kind,
    long                   Tick,
    IReadOnlyList<int>     ParticipantIds,
    string                 Location,
    string                 Description,
    bool                   Persistent,
    string?                PhysicalManifestEntityId
);
