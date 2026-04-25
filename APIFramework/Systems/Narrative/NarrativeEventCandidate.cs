using System.Collections.Generic;

namespace APIFramework.Systems.Narrative;

/// <summary>
/// An observable moment that the narrative detector believes is worth surfacing.
/// Carries structured fields for tooling and a human-readable Detail string (≤ 280 chars).
/// Candidates are emitted open-loop — persistence and filtering are chronicle concerns (WP-1.9.A).
/// </summary>
public sealed record NarrativeEventCandidate(
    long                  Tick,
    NarrativeEventKind    Kind,
    IReadOnlyList<int>    ParticipantIds,
    string?               RoomId,
    string                Detail
);
