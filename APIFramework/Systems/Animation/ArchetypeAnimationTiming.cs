namespace APIFramework.Systems.Animation;

/// <summary>Per-archetype animation speed multipliers loaded from archetype-animation-timing.json.</summary>
public sealed record ArchetypeAnimationTiming(
    string Archetype,
    float  WalkSpeedMult,
    float  EatSpeedMult,
    float  TalkGesturalRate);
