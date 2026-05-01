using System;
using System.Collections.Generic;

namespace APIFramework.Systems.Animation;

/// <summary>
/// Provides per-archetype animation speed multipliers.
/// Loaded from docs/c2-content/animation/archetype-animation-timing.json at startup.
/// Falls back to 1.0 for any archetype not listed.
/// </summary>
public sealed class AnimationTimingCatalog
{
    private readonly Dictionary<string, ArchetypeAnimationTiming> _byArchetype;

    public AnimationTimingCatalog(IEnumerable<ArchetypeAnimationTiming> entries)
    {
        _byArchetype = new Dictionary<string, ArchetypeAnimationTiming>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
            _byArchetype[e.Archetype] = e;
    }

    public float GetWalkSpeedMult(string archetypeId) =>
        _byArchetype.TryGetValue(archetypeId, out var e) ? e.WalkSpeedMult : 1f;

    public float GetEatSpeedMult(string archetypeId) =>
        _byArchetype.TryGetValue(archetypeId, out var e) ? e.EatSpeedMult : 1f;

    public float GetTalkGesturalRate(string archetypeId) =>
        _byArchetype.TryGetValue(archetypeId, out var e) ? e.TalkGesturalRate : 1f;

    /// <summary>Default catalog with the four canonical archetypes from archetype-animation-timing.json.</summary>
    public static AnimationTimingCatalog Default { get; } = new(new[]
    {
        new ArchetypeAnimationTiming("the-old-hand",  WalkSpeedMult: 0.85f, EatSpeedMult: 0.80f, TalkGesturalRate: 0.90f),
        new ArchetypeAnimationTiming("the-newbie",    WalkSpeedMult: 1.15f, EatSpeedMult: 1.20f, TalkGesturalRate: 1.25f),
        new ArchetypeAnimationTiming("the-climber",   WalkSpeedMult: 1.20f, EatSpeedMult: 1.10f, TalkGesturalRate: 1.15f),
        new ArchetypeAnimationTiming("the-hermit",    WalkSpeedMult: 0.80f, EatSpeedMult: 0.95f, TalkGesturalRate: 0.70f),
    });
}
