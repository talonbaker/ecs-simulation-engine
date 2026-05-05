using System;
using APIFramework.Cast.Internal;

namespace APIFramework.Cast;

/// <summary>
/// Probabilistic name + title generator for the office sim's cast.
/// Direct port of Talon's HTML/JS roster generator
/// (<c>~/talonbaker.github.io/name-face-gen/data.js</c>) — six-tier rarity system
/// (Common 55% / Uncommon 27% / Rare 12% / Epic 4% / Legendary 1.5% / Mythic 0.5%),
/// modular title builder, fusion grammar with consonant-collapse cleanup.
///
/// API is deterministic + seedable: the seam for the future "reroll for a better hire"
/// loot-box mechanic is one wrapper away — call <see cref="Generate(Random, System.Nullable{CastGender}, System.Nullable{CastNameTier})"/>
/// with a fresh <see cref="Random"/> instance per reroll.
///
/// JS-vs-C# RNG note: the JS source's <c>Math.random()</c> and .NET's <see cref="Random"/>
/// use different algorithms; matched seeds will NOT produce identical names across the two
/// implementations. Behavioral equivalence (same tier distributions over large N; same logical
/// structure per tier) is the bar, not bit-exact output. The JS sandbox stays the spec for
/// behavior; this library matches behavior, not raw output.
/// </summary>
public sealed class CastNameGenerator
{
    private readonly CastNameData _data;

    /// <summary>Construct with the supplied catalog (typically loaded via <see cref="CastNameDataLoader"/>).</summary>
    public CastNameGenerator(CastNameData data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>
    /// Direct accessor for the underlying catalog. Used by economy wrappers
    /// (<c>HireRerollService</c>) that need to read the default tier thresholds before
    /// applying perk-driven biases.
    /// </summary>
    public CastNameData Data => _data;

    /// <summary>
    /// Generate a name using a per-call override of the tier thresholds. Used by the
    /// hire-reroll economy (WP-4.0.N) to apply player perk biases that tilt the rarity
    /// curve without mutating the catalog. Other arguments behave identically to
    /// <see cref="Generate(Random, System.Nullable{CastGender}, System.Nullable{CastNameTier})"/>.
    /// </summary>
    public CastNameResult GenerateWithThresholds(
        Random              rng,
        TierThresholdsDto   thresholds,
        CastGender?         gender = null)
    {
        if (rng is null)        throw new ArgumentNullException(nameof(rng));
        if (thresholds is null) throw new ArgumentNullException(nameof(thresholds));

        var g     = gender ?? PickGender(rng);
        var first = Internal.FusionBuilder.Pick(_data.FirstNames[GenderKey(g)], rng);
        var tier  = Internal.TierRoller.Roll(rng.NextDouble(), thresholds);

        return tier switch
        {
            CastNameTier.Mythic    => BuildMythic(g, first, rng),
            CastNameTier.Legendary => BuildLegendary(g, first, rng),
            CastNameTier.Epic      => BuildEpic(g, first, rng),
            CastNameTier.Rare      => BuildRare(g, first, rng),
            CastNameTier.Uncommon  => BuildUncommon(g, first, rng),
            _                      => BuildCommon(g, first, rng),
        };
    }

    /// <summary>
    /// Generates a name using the supplied <see cref="Random"/> instance — the canonical entry point.
    /// Tests pass a seeded RNG for determinism; future reroll wrappers pass the player's
    /// session RNG so reroll sequences share state.
    /// </summary>
    /// <param name="rng">RNG used for every roll in the generation pipeline.</param>
    /// <param name="gender">When null, picks uniformly from male/female/neutral.</param>
    /// <param name="forcedTier">Debug override; bypasses the tier roll. When null, rolls per <see cref="CastNameData.TierThresholds"/>.</param>
    public CastNameResult Generate(
        Random        rng,
        CastGender?   gender     = null,
        CastNameTier? forcedTier = null)
    {
        if (rng is null) throw new ArgumentNullException(nameof(rng));

        var g     = gender ?? PickGender(rng);
        var first = FusionBuilder.Pick(_data.FirstNames[GenderKey(g)], rng);
        var tier  = forcedTier ?? TierRoller.Roll(rng.NextDouble(), _data.TierThresholds);

        return tier switch
        {
            CastNameTier.Mythic    => BuildMythic(g, first, rng),
            CastNameTier.Legendary => BuildLegendary(g, first, rng),
            CastNameTier.Epic      => BuildEpic(g, first, rng),
            CastNameTier.Rare      => BuildRare(g, first, rng),
            CastNameTier.Uncommon  => BuildUncommon(g, first, rng),
            _                      => BuildCommon(g, first, rng),
        };
    }

    /// <summary>Convenience: generate from a seed (deterministic). Reroll = call with a different seed.</summary>
    public CastNameResult Generate(int seed, CastGender? gender = null, CastNameTier? forcedTier = null)
        => Generate(new Random(seed), gender, forcedTier);

    /// <summary>Convenience: generate with a fresh non-deterministic RNG.</summary>
    public CastNameResult Generate(CastGender? gender = null, CastNameTier? forcedTier = null)
        => Generate(new Random(), gender, forcedTier);

    /// <summary>
    /// Convenience: generate name + matching badge in one call. Uses the same RNG instance
    /// for both, so the (Name, Badge) pair is reproducible from a single seed. Equivalent
    /// to calling <see cref="Generate(Random, System.Nullable{CastGender}, System.Nullable{CastNameTier})"/>
    /// then <c>new CastBadgeGenerator(_data).Generate(rng, name)</c>.
    /// </summary>
    public (CastNameResult Name, CastBadgeResult Badge) GenerateWithBadge(
        Random        rng,
        CastGender?   gender     = null,
        CastNameTier? forcedTier = null)
    {
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        var name  = Generate(rng, gender, forcedTier);
        var badge = new CastBadgeGenerator(_data).Generate(rng, name);
        return (name, badge);
    }

    /// <summary>Convenience: same as the seedable <see cref="GenerateWithBadge(Random, System.Nullable{CastGender}, System.Nullable{CastNameTier})"/> with a fresh non-deterministic RNG.</summary>
    public (CastNameResult Name, CastBadgeResult Badge) GenerateWithBadge(
        CastGender?   gender     = null,
        CastNameTier? forcedTier = null)
        => GenerateWithBadge(new Random(), gender, forcedTier);

    // ── Per-tier builders (direct port of generateName branches) ─────────────────

    private CastNameResult BuildCommon(CastGender g, string first, Random rng)
    {
        var sur = FusionBuilder.Pick(_data.StaticLastNames, rng);
        return new CastNameResult($"{first} {sur}", CastNameTier.Common, g, first,
            Surname: sur, Title: null, LegendaryRoot: null, LegendaryTitle: null, CorporateTitle: null);
    }

    private CastNameResult BuildUncommon(CastGender g, string first, Random rng)
    {
        var sur = FusionBuilder.CleanFusion(
            FusionBuilder.Pick(_data.FusionPrefixes, rng) + FusionBuilder.Pick(_data.FusionSuffixes, rng));
        var title = TitleBuilder.Build(CastNameTier.Uncommon, _data, rng);
        return new CastNameResult($"{first} {sur}", CastNameTier.Uncommon, g, first,
            Surname: sur, Title: title, LegendaryRoot: null, LegendaryTitle: null, CorporateTitle: null);
    }

    private CastNameResult BuildRare(CastGender g, string first, Random rng)
    {
        var sur1 = FusionBuilder.CleanFusion(
            FusionBuilder.BuildSurname(_data, rng) + FusionBuilder.Pick(_data.FusionSuffixes, rng));
        string surname;
        if (rng.NextDouble() < 0.15)
        {
            var sur2 = FusionBuilder.BuildShortSurnameHalf(_data, rng);
            surname = $"{sur1}-{sur2}";
        }
        else
        {
            surname = sur1;
        }
        var title = TitleBuilder.Build(CastNameTier.Rare, _data, rng);
        return new CastNameResult($"{first} {surname}", CastNameTier.Rare, g, first,
            Surname: surname, Title: title, LegendaryRoot: null, LegendaryTitle: null, CorporateTitle: null);
    }

    private CastNameResult BuildEpic(CastGender g, string first, Random rng)
    {
        var corp = FusionBuilder.Pick(_data.CorporateTitles, rng);
        string surname = rng.NextDouble() < 0.30
            ? $"{FusionBuilder.BuildShortSurnameHalf(_data, rng)}-{FusionBuilder.BuildShortSurnameHalf(_data, rng)}"
            : FusionBuilder.BuildShortSurnameHalf(_data, rng);
        var display = $"{corp} {first} {surname}";
        return new CastNameResult(display, CastNameTier.Epic, g, first,
            Surname: surname, Title: corp, LegendaryRoot: null, LegendaryTitle: null, CorporateTitle: corp);
    }

    private CastNameResult BuildLegendary(CastGender g, string first, Random rng)
    {
        var root = FusionBuilder.Pick(_data.LegendaryRoots[GenderKey(g)], rng);

        if (rng.NextDouble() < 0.5)
        {
            // divine path
            var title   = FusionBuilder.Pick(_data.LegendaryTitles, rng);
            var display = $"{root} {title}";
            return new CastNameResult(display, CastNameTier.Legendary, g, first,
                Surname: null, Title: title, LegendaryRoot: root, LegendaryTitle: title, CorporateTitle: null);
        }
        else
        {
            // hybrid path
            var corp    = FusionBuilder.Pick(_data.CorporateTitles, rng);
            var surname = rng.NextDouble() < 0.25
                ? $"{FusionBuilder.BuildShortSurnameHalf(_data, rng)}-{FusionBuilder.BuildShortSurnameHalf(_data, rng)}"
                : FusionBuilder.BuildShortSurnameHalf(_data, rng);
            var display = $"{corp} {root} {surname}";
            return new CastNameResult(display, CastNameTier.Legendary, g, first,
                Surname: surname, Title: corp, LegendaryRoot: root, LegendaryTitle: null, CorporateTitle: corp);
        }
    }

    private CastNameResult BuildMythic(CastGender g, string first, Random rng)
    {
        var root    = FusionBuilder.Pick(_data.LegendaryRoots[GenderKey(g)], rng);
        var title   = FusionBuilder.Pick(_data.LegendaryTitles, rng);
        var corp    = FusionBuilder.Pick(_data.CorporateTitles, rng);
        var display = $"{corp} {root}, {title}";
        return new CastNameResult(display, CastNameTier.Mythic, g, first,
            Surname: null, Title: title, LegendaryRoot: root, LegendaryTitle: title, CorporateTitle: corp);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static CastGender PickGender(Random rng)
    {
        var r = rng.Next(3);
        return r switch
        {
            0 => CastGender.Male,
            1 => CastGender.Female,
            _ => CastGender.Neutral,
        };
    }

    private static string GenderKey(CastGender g) => g switch
    {
        CastGender.Male    => "male",
        CastGender.Female  => "female",
        _                  => "neutral",
    };
}
