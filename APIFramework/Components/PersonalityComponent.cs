using System;

namespace APIFramework.Components;

/// <summary>Vocabulary register — constrains dialogue generation style without prescribing lines.</summary>
public enum VocabularyRegister { Formal, Casual, Crass, Clipped, Academic, Folksy }

/// <summary>
/// Stable-for-the-save personality state.
/// Big Five traits each –2..+2. VocabularyRegister controls dialogue style.
/// CurrentMood is a persistent self-perceived label (distinct from MoodComponent's
/// short-lived Plutchik emotions). Max 32 chars; constructor and setter truncate.
/// </summary>
public struct PersonalityComponent
{
    public int Openness;
    public int Conscientiousness;
    public int Extraversion;
    public int Agreeableness;
    public int Neuroticism;

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
}
