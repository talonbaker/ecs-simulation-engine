using System;
using System.Linq;
using APIFramework.Cast;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Bootstrap;

/// <summary>
/// Auto-naming layer for runtime NPC spawning (WP-4.0.K). Wraps
/// <see cref="CastNameGenerator"/> (WP-4.0.M) with collision-retry against names
/// currently in use by live NPCs. Collisions are vanishingly rare in practice
/// (the fusion grammar produces a huge cardinality of names), but we retry
/// up to <see cref="MaxRerollAttempts"/> times before falling back to a
/// numeric suffix.
///
/// Typical flow (called by author-mode UI / NPC palette tool):
///   var result = pool.GenerateUniqueName(CastGender.Female);
///   var npcId  = api.CreateNpc(roomId, x, y, archetypeId, result.DisplayName);
/// </summary>
public sealed class CastNamePool
{
    /// <summary>Number of reroll attempts before falling back to a numeric suffix.</summary>
    public const int MaxRerollAttempts = 5;

    private readonly EntityManager      _em;
    private readonly CastNameGenerator  _generator;
    private readonly Random             _rng;

    public CastNamePool(EntityManager em, CastNameGenerator generator, Random? rng = null)
    {
        _em        = em        ?? throw new ArgumentNullException(nameof(em));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _rng       = rng       ?? new Random();
    }

    /// <summary>
    /// Generate a name not currently used by any live NPC (entity with
    /// <see cref="IdentityComponent"/>). Returns the full <see cref="CastNameResult"/>
    /// so the caller can record the tier on the spawned NPC.
    ///
    /// If <see cref="MaxRerollAttempts"/> consecutive rolls collide with existing
    /// names, falls back to <c>{archetypeFallback}-{n}</c> where n is the next free
    /// integer suffix.
    /// </summary>
    public CastNameResult GenerateUniqueName(CastGender? gender = null, string archetypeFallback = "npc")
    {
        for (int attempt = 0; attempt < MaxRerollAttempts; attempt++)
        {
            var candidate = _generator.Generate(_rng, gender);
            if (!IsNameTaken(candidate.DisplayName))
                return candidate;
        }

        // Numeric fallback — rare event. Returns a "common" tier result with the synthetic name.
        var fallbackName = AssignNumericSuffix(archetypeFallback);
        return new CastNameResult(
            DisplayName:    fallbackName,
            Tier:           CastNameTier.Common,
            Gender:         gender ?? CastGender.Neutral,
            FirstName:      fallbackName,
            Surname:        null,
            Title:          null,
            LegendaryRoot:  null,
            LegendaryTitle: null,
            CorporateTitle: null);
    }

    /// <summary>True if any live entity's IdentityComponent.Name equals <paramref name="name"/>.</summary>
    public bool IsNameTaken(string name) =>
        _em.Query<IdentityComponent>()
           .Any(e => string.Equals(e.Get<IdentityComponent>().Name, name, StringComparison.Ordinal));

    private string AssignNumericSuffix(string baseLabel)
    {
        for (int n = 1; n < int.MaxValue; n++)
        {
            var candidate = $"{baseLabel}-{n}";
            if (!IsNameTaken(candidate)) return candidate;
        }
        // Exhausted int.MaxValue — should never happen in practice.
        return $"{baseLabel}-{Guid.NewGuid():N}";
    }
}
