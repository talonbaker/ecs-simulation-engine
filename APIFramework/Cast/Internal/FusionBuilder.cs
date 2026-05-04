using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace APIFramework.Cast.Internal;

/// <summary>
/// Surname construction + the consonant-collapse cleanup pass. Direct port of the JS
/// <c>buildSurname</c> / <c>buildShortSurnameHalf</c> / <c>cleanFusion</c> helpers.
/// </summary>
internal static class FusionBuilder
{
    private static readonly Regex _tripleConsonant =
        new(@"([bcdfghjklmnpqrstvwxz])\1{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _ttb = new("ttb", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _ckk = new("ckk", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _ssk = new("ssk", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _kkk = new("kkk", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _sss = new("sss", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Collapses 3+ identical consonants and known awkward clusters per JS source.</summary>
    public static string CleanFusion(string s)
    {
        s = _tripleConsonant.Replace(s, "$1$1");
        s = _ttb.Replace(s, "tb");
        s = _ckk.Replace(s, "ck");
        s = _ssk.Replace(s, "sk");
        s = _kkk.Replace(s, "k");
        s = _sss.Replace(s, "ss");
        return s;
    }

    /// <summary>
    /// 50/50: full static last name, or fusion prefix + suffix.
    /// </summary>
    public static string BuildSurname(CastNameData d, Random rng)
    {
        var useStatic = rng.NextDouble() < 0.5;
        if (useStatic) return Pick(d.StaticLastNames, rng);
        var root = Pick(d.FusionPrefixes, rng);
        return CleanFusion(root + Pick(d.FusionSuffixes, rng));
    }

    /// <summary>
    /// Short surname half — used for hyphenated names so they don't get insanely long.
    /// Prefers shorter static names (≤8 chars) and small prefix+suffix combos (≤5 + ≤4).
    /// </summary>
    public static string BuildShortSurnameHalf(CastNameData d, Random rng)
    {
        var useStatic = rng.NextDouble() < 0.5;
        if (useStatic)
        {
            var shortStatics = d.StaticLastNames.Where(n => n.Length <= 8).ToArray();
            if (shortStatics.Length == 0) shortStatics = d.StaticLastNames;     // fallback if catalog is unusual
            return Pick(shortStatics, rng);
        }
        var shortPrefixes = d.FusionPrefixes.Where(p => p.Length <= 5).ToArray();
        if (shortPrefixes.Length == 0) shortPrefixes = d.FusionPrefixes;
        var shortSuffixes = d.FusionSuffixes.Where(s => s.Length <= 4).ToArray();
        if (shortSuffixes.Length == 0) shortSuffixes = d.FusionSuffixes;
        return CleanFusion(Pick(shortPrefixes, rng) + Pick(shortSuffixes, rng));
    }

    public static string Pick(string[] arr, Random rng) => arr[rng.Next(arr.Length)];
}
