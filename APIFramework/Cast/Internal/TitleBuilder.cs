using System;
using System.Linq;

namespace APIFramework.Cast.Internal;

/// <summary>
/// Per-tier title construction. Direct port of the JS <c>generateTitle</c> switch.
/// Returns <c>null</c> when the tier's title-chance roll fails (or for Common, never).
/// </summary>
internal static class TitleBuilder
{
    public static string? Build(CastNameTier tier, CastNameData d, Random rng)
    {
        var t = d.TitleTiers;

        switch (tier)
        {
            case CastNameTier.Common:
                return null;

            case CastNameTier.Uncommon:
            {
                if (rng.NextDouble() > 0.15) return null;
                return PickFromAny(rng, t.Rank.Mundane, t.Rank.Silly);
            }

            case CastNameTier.Rare:
            {
                if (rng.NextDouble() > 0.40) return null;
                var rank = PickFromAny(rng, t.Rank.Mundane, t.Rank.Silly);
                if (rng.NextDouble() < 0.5) return rank;
                var fn = PickFromAny(rng, t.Function.Mundane, t.Function.Silly);
                return $"{rank} {fn}";
            }

            case CastNameTier.Epic:
            {
                if (rng.NextDouble() > 0.80) return null;
                var rank   = PickFromAny(rng, t.Rank.Mundane, t.Rank.Silly);
                var domain = PickFromAny(rng, t.Domain.Mundane, t.Domain.Silly);
                var fn     = PickFromAny(rng, t.Function.Mundane, t.Function.Silly);
                return rng.NextDouble() < 0.4
                    ? $"{rank} {domain} {fn}"
                    : $"{rank} {fn}";
            }

            case CastNameTier.Legendary:
            {
                var rank   = Pick(t.Rank.HighStatus, rng);
                var domain = Pick(t.Domain.HighStatus, rng);
                var fn     = Pick(t.Function.HighStatus, rng);
                return $"{rank} {domain} {fn}";
            }

            case CastNameTier.Mythic:
            {
                var rank    = Pick(t.Rank.HighStatus, rng);
                var domain  = Pick(t.Domain.HighStatus, rng);
                var fn      = Pick(t.Function.HighStatus, rng);
                // pick a second domain that may equal the first (the JS source allows repetition)
                var domain2 = Pick(t.Domain.HighStatus, rng);
                return $"{rank} {domain} {fn} of {domain2}";
            }
        }

        return null;
    }

    private static string Pick(string[] arr, Random rng) => arr[rng.Next(arr.Length)];

    private static string PickFromAny(Random rng, params string[][] arrays)
    {
        var combined = arrays.SelectMany(a => a).ToArray();
        return combined[rng.Next(combined.Length)];
    }
}
