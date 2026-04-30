using System;

namespace APIFramework.Components;

/// <summary>Vocabulary register — constrains dialogue generation style without prescribing lines.</summary>
public enum VocabularyRegister
{
    /// <summary>Polite, structured, professional speech.</summary>
    Formal,
    /// <summary>Everyday relaxed speech.</summary>
    Casual,
    /// <summary>Coarse, vulgar, blunt speech.</summary>
    Crass,
    /// <summary>Terse, low-syllable, business-like speech.</summary>
    Clipped,
    /// <summary>Erudite, jargon-heavy, intellectual speech.</summary>
    Academic,
    /// <summary>Warm, idiomatic, regional/folk speech.</summary>
    Folksy
}

/// <summary>
/// Stable-for-the-save personality state.
/// Big Five traits each –2..+2. VocabularyRegister controls dialogue style.
/// CurrentMood is a persistent self-perceived label (distinct from MoodComponent's
/// short-lived Plutchik emotions). Max 32 chars; constructor and setter truncate.
/// </summary>
public struct PersonalityComponent
{
    /// <summary>Big Five Openness, –2..+2 (clamped on assign).</summary>
    public int Openness;
    /// <summary>Big Five Conscientiousness, –2..+2 (clamped on assign).</summary>
    public int Conscientiousness;
    /// <summary>Big Five Extraversion, –2..+2 (clamped on assign).</summary>
    public int Extraversion;
    /// <summary>Big Five Agreeableness, –2..+2 (clamped on assign).</summary>
    public int Agreeableness;
    /// <summary>Big Five Neuroticism, –2..+2 (clamped on assign).</summary>
    public int Neuroticism;

    /// <summary>Vocabulary register that constrains dialogue style.</summary>
    public VocabularyRegister VocabularyRegister;

    private string? _currentMood;

    /// <summary>
    /// Persistent self-perceived mood label. NOT the same as MoodComponent emotions.
    /// Capped at 32 characters; longer values are silently truncated.
    /// </summary>
    public string CurrentMood
    {
        get => _currentMood ?? string.Empty;
        set => _currentMood = value is { Length: > 32 }
            ? value.Substring(0, 32)
            : value ?? string.Empty;
    }

    /// <summary>
    /// Constructs a personality. Big Five values are clamped to [–2, +2];
    /// <paramref name="currentMood"/> is truncated to 32 characters.
    /// </summary>
    public PersonalityComponent(
        int openness, int conscientiousness, int extraversion,
        int agreeableness, int neuroticism,
        VocabularyRegister register = VocabularyRegister.Casual,
        string? currentMood = null)
    {
        Openness          = Math.Clamp(openness,          -2, 2);
        Conscientiousness = Math.Clamp(conscientiousness, -2, 2);
        Extraversion      = Math.Clamp(extraversion,      -2, 2);
        Agreeableness     = Math.Clamp(agreeableness,     -2, 2);
        Neuroticism       = Math.Clamp(neuroticism,       -2, 2);
        VocabularyRegister = register;
        _currentMood      = currentMood is { Length: > 32 }
            ? currentMood.Substring(0, 32)
            : currentMood;
    }

    /// <summary>
    /// Parses a register name string (from the archetype catalog) to the enum value.
    /// Returns <see cref="VocabularyRegister.Casual"/> for unknown values.
    /// </summary>
    public static VocabularyRegister ParseRegister(string name) => name switch
    {
        "formal"   => VocabularyRegister.Formal,
        "casual"   => VocabularyRegister.Casual,
        "crass"    => VocabularyRegister.Crass,
        "clipped"  => VocabularyRegister.Clipped,
        "academic" => VocabularyRegister.Academic,
        "folksy"   => VocabularyRegister.Folksy,
        _          => VocabularyRegister.Casual
    };
}
