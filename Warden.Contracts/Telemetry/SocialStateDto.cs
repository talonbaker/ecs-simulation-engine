using System.Collections.Generic;

namespace Warden.Contracts.Telemetry;

// -- Social state (per-entity self-state) --------------------------------------

public sealed record SocialStateDto
{
    public DrivesDto?                          Drives             { get; init; }
    public WillpowerDto?                       Willpower          { get; init; }
    public IReadOnlyList<InhibitionDto>?       Inhibitions        { get; init; }
    public IReadOnlyList<PersonalityTraitDto>? PersonalityTraits  { get; init; }
    public string?                             CurrentMood        { get; init; }
    public VocabularyRegister?                 VocabularyRegister { get; init; }
}

public sealed record DrivesDto
{
    public DriveValueDto Belonging  { get; init; } = default!;
    public DriveValueDto Status     { get; init; } = default!;
    public DriveValueDto Affection  { get; init; } = default!;
    public DriveValueDto Irritation { get; init; } = default!;
    public DriveValueDto Attraction { get; init; } = default!;
    public DriveValueDto Trust      { get; init; } = default!;
    public DriveValueDto Suspicion  { get; init; } = default!;
    public DriveValueDto Loneliness { get; init; } = default!;
}

public sealed record DriveValueDto
{
    public int Current  { get; init; }
    public int Baseline { get; init; }
}

public sealed record WillpowerDto
{
    public int Current  { get; init; }
    public int Baseline { get; init; }
}

public sealed record InhibitionDto
{
    public InhibitionClass     Class     { get; init; }
    public int                 Strength  { get; init; }
    public InhibitionAwareness Awareness { get; init; }
}

public sealed record PersonalityTraitDto
{
    public BigFiveDimension Dimension { get; init; }
    public int              Value     { get; init; }
}

// -- Enums ---------------------------------------------------------------------

/// <summary>Vocabulary register. Serialises as camelCase string.</summary>
public enum VocabularyRegister { Formal, Casual, Crass, Clipped, Academic, Folksy }

/// <summary>Big Five personality dimension. Serialises as camelCase string.</summary>
public enum BigFiveDimension { Openness, Conscientiousness, Extraversion, Agreeableness, Neuroticism }

/// <summary>Action class blocked by an inhibition. Serialises as camelCase string.</summary>
public enum InhibitionClass
{
    Infidelity,
    Confrontation,
    BodyImageEating,
    PublicEmotion,
    PhysicalIntimacy,
    InterpersonalConflict,
    RiskTaking,
    Vulnerability
}

/// <summary>Whether the NPC is aware of their own inhibition. Serialises as camelCase string.</summary>
public enum InhibitionAwareness { Known, Hidden }
