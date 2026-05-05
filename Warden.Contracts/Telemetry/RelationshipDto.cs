using System;
using System.Collections.Generic;

namespace Warden.Contracts.Telemetry;

// -- Relationship (per-pair entity) --------------------------------------------

public sealed record RelationshipDto
{
    public string                            Id              { get; init; } = string.Empty;
    public string                            ParticipantA    { get; init; } = string.Empty;
    public string                            ParticipantB    { get; init; } = string.Empty;
    public IReadOnlyList<RelationshipPattern> Patterns       { get; init; } = Array.Empty<RelationshipPattern>();
    public int                               Intensity       { get; init; }
    public IReadOnlyList<string>             HistoryEventIds { get; init; } = Array.Empty<string>();
}

// -- Enum ----------------------------------------------------------------------

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
