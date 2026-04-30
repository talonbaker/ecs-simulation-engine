using System.Collections.Generic;

namespace APIFramework.Systems.Narrative;

/// <summary>
/// An observable moment that the narrative detector believes is worth surfacing.
/// Carries structured fields for tooling and a human-readable Detail string (≤ 280 chars).
/// Candidates are emitted open-loop — persistence and filtering are chronicle concerns (WP-1.9.A).
/// </summary>
/// <param name="Tick">The simulation tick on which the candidate was generated.</param>
/// <param name="Kind">Discriminator for the kind of narrative moment.</param>
/// <param name="ParticipantIds">
/// EntityIntId values of the entities involved. The first entry is the primary subject
/// (e.g. the deceased for death events, the speaker for conversation events); subsequent
/// entries are witnesses or secondary participants.
/// </param>
/// <param name="RoomId">Optional room identifier where the event occurred. Null when not room-bound.</param>
/// <param name="Detail">Human-readable summary, truncated to NarrativeConfig.CandidateDetailMaxLength.</param>
/// <seealso cref="NarrativeEventBus"/>
/// <seealso cref="NarrativeEventKind"/>
/// <seealso cref="NarrativeEventDetector"/>
public sealed record NarrativeEventCandidate(
    long                  Tick,
    NarrativeEventKind    Kind,
    IReadOnlyList<int>    ParticipantIds,
    string?               RoomId,
    string                Detail
);
