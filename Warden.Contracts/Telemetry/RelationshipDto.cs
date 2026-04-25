using System;
using System.Collections.Generic;

namespace Warden.Contracts.Telemetry;

// ── Relationship (per-pair entity) ────────────────────────────────────────────

public sealed record RelationshipDto
{
    public string                         Id              { get; init; } = string.Empty;
    public string                         ParticipantA    { get; init; } = string.Empty;
    public string                         ParticipantB    { get; init; } = string.Empty;
    public IReadOnlyList<RelationshipPattern> Patterns    { get; init; } = Array.Empty<RelationshipPattern>();
    public PairDrivesDto                  PairDrives      { get; init; } = default!;
    public int                            Intensity       { get; init; }
    public IReadOnlyList<string>          HistoryEventIds { get; init; } = Array.Empty<string>();
}

public sealed record PairDrivesDto
{
    public int Attraction { get; init; }
    public int Trust      { get; init; }
    public int Suspicion  { get; init; }
    public int Jealousy   { get; init; }
}

// ── Enum ──────────────────────────────────────────────────────────────────────

/// <summary>
/// Relationship pattern from the cast-bible pattern library.
/// Serialises as camelCase string (e.g. <c>OldFlame</c> → <c>"oldFlame"</c>).
/// </summary>
public enum RelationshipPattern
{
    Rival,
    OldFlame,
    ActiveAffair,
    SecretCrush,
    Mentor,
    Mentee,
    BossOf,
    ReportTo,
    Friend,
    AlliesOfConvenience,
    SleptWithSpouse,
    Confidant,
    TheThingNobodyTalksAbout
}
