using System;
using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>
/// Relationship pattern from the cast-bible pattern library.
/// Mirrors Warden.Contracts.Telemetry.RelationshipPattern (13 values).
/// </summary>
public enum RelationshipPattern
{
    /// <summary>The two NPCs are rivals.</summary>
    Rival,
    /// <summary>Past romantic involvement that has ended.</summary>
    OldFlame,
    /// <summary>An ongoing affair.</summary>
    ActiveAffair,
    /// <summary>One-sided unspoken attraction.</summary>
    SecretCrush,
    /// <summary>This NPC mentors the other.</summary>
    Mentor,
    /// <summary>This NPC is mentored by the other.</summary>
    Mentee,
    /// <summary>This NPC is the other's boss.</summary>
    BossOf,
    /// <summary>This NPC reports to the other.</summary>
    ReportTo,
    /// <summary>Mutual friendship.</summary>
    Friend,
    /// <summary>Allied for convenience rather than affinity.</summary>
    AlliesOfConvenience,
    /// <summary>One of the pair has slept with the other's spouse.</summary>
    SleptWithSpouse,
    /// <summary>Trusted confidant.</summary>
    Confidant,
    /// <summary>A history-dense secret pattern between the two.</summary>
    TheThingNobodyTalksAbout
}

/// <summary>
/// Lives on the relationship entity (not on either participant).
/// ParticipantA/B are canonicalized at construction: lower int id first.
/// Patterns max 2; Intensity 0–100.
/// </summary>
public struct RelationshipComponent
{
    /// <summary>Canonical lower entity-id participant.</summary>
    public int ParticipantA;   // lower entity-id (canonical)
    /// <summary>Canonical higher entity-id participant.</summary>
    public int ParticipantB;   // higher entity-id (canonical)
    /// <summary>How loud this relationship is in either NPC's life, in [0, 100].</summary>
    public int Intensity;      // 0–100; how loud this relationship is in either NPC's life

    private readonly IReadOnlyList<RelationshipPattern>? _patterns;

    /// <summary>The cast-pattern list for this relationship (max 2). Empty when default-initialised.</summary>
    public IReadOnlyList<RelationshipPattern> Patterns =>
        _patterns ?? Array.Empty<RelationshipPattern>();

    /// <summary>
    /// Constructs a relationship between <paramref name="a"/> and <paramref name="b"/>.
    /// Participants are canonicalised (lower id → A); throws if more than two patterns are supplied.
    /// </summary>
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
