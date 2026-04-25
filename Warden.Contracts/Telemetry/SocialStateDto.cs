using System.Collections.Generic;

namespace Warden.Contracts.Telemetry;

// ── Social state (per-entity self-state) ──────────────────────────────────────

public sealed record SocialStateDto
{
    public SelfDrivesDto?                      SelfDrives         { get; init; }
    public IReadOnlyList<PersonalityTraitDto>? PersonalityTraits  { get; init; }
    public string?                             CurrentMood        { get; init; }
    public VocabularyRegister?                 VocabularyRegister { get; init; }
}

public sealed record SelfDrivesDto
{
    public int Belonging  { get; init; }
    public int Status     { get; init; }
    public int Affection  { get; init; }
    public int Irritation { get; init; }
    public int Loneliness { get; init; }
}

public sealed record PersonalityTraitDto
{
    public BigFiveDimension Dimension { get; init; }
    public int              Value     { get; init; }
}

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>Vocabulary register. Serialises as camelCase string.</summary>
public enum VocabularyRegister { Formal, Casual, Crass, Clipped, Academic, Folksy }

/// <summary>Big Five personality dimension. Serialises as camelCase string.</summary>
public enum BigFiveDimension { Openness, Conscientiousness, Extraversion, Agreeableness, Neuroticism }
