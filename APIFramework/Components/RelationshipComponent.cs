using System;
using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>
/// Relationship pattern from the cast-bible pattern library.
/// Mirrors Warden.Contracts.Telemetry.RelationshipPattern (13 values).
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

/// <summary>
/// Lives on the relationship entity (not on either participant).
/// ParticipantA/B are canonicalized at construction: lower int id first.
/// Patterns max 2; Intensity 0–100.
/// </summary>
public struct RelationshipComponent
{
    public int ParticipantA;   // lower entity-id (canonical)
    public int ParticipantB;   // higher entity-id (canonical)
    public int Intensity;      // 0–100; how loud this relationship is in either NPC's life

    private readonly IReadOnlyList<RelationshipPattern>? _patterns;

    public IReadOnlyList<RelationshipPattern> Patterns =>
        _patterns ?? Array.Empty<RelationshipPattern>();

    public RelationshipComponent(
        int a, int b,
        IReadOnlyList<RelationshipPattern>? patterns = null,
        int intensity = 50)
    {
        // Canonical ordering: lower id first
        ParticipantA = Math.Min(a, b);
        ParticipantB = Math.Max(a, b);

        if (patterns is { Count: > 2 })
            throw new ArgumentException(
                "A relationship supports at most 2 patterns.", nameof(patterns));

        _patterns = patterns;
        Intensity  = Math.Clamp(intensity, 0, 100);
    }
}
