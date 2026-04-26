using System.Collections.Generic;

namespace APIFramework.Systems.Chronicle;

/// <summary>
/// Engine-internal record for a single persistent chronicle event.
/// Produced by <see cref="PersistenceThresholdDetector"/> and stored in <see cref="ChronicleService"/>.
/// </summary>
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
