using System;
using APIFramework.Cast.Internal;

namespace APIFramework.Cast;

/// <summary>
/// Per-NPC badge generator. Direct port of <c>generateBadge()</c> + <c>findDeptStamp()</c>
/// from <c>~/talonbaker.github.io/name-face-gen/data.js</c>. Pure data layer — UI consumers
/// (future hire-screen, inspector tab, bulletin board) format the populated fields into the
/// visual badge card.
///
/// Same deterministic-seedable pattern as <see cref="CastNameGenerator"/>: pass a seeded
/// <see cref="Random"/> for reproducibility; pass a fresh one for each independent badge.
/// </summary>
public sealed class CastBadgeGenerator
{
    private readonly CastNameData _data;

    public CastBadgeGenerator(CastNameData data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>
    /// Generates a badge for the given <paramref name="name"/> using the supplied <paramref name="rng"/>.
    /// Per-tier composition matches the JS source's <c>generateBadge()</c> branches.
    /// </summary>
    public CastBadgeResult Generate(Random rng, CastNameResult name)
    {
        if (rng is null)  throw new ArgumentNullException(nameof(rng));
        if (name is null) throw new ArgumentNullException(nameof(name));

        // Per the JS source, the badge title is re-rolled via the modular TitleBuilder —
        // it is NOT inherited from name.Title (which holds the corporate title used in
        // the display name, when any). The two title sources are deliberately distinct.
        var modularTitle = TitleBuilder.Build(name.Tier, _data, rng);

        var bf = _data.BadgeFlair         ?? new BadgeFlairDto();
        var mf = bf.Mundane               ?? new BadgeMundaneDto();
        var hf = bf.HighStatus            ?? new BadgeHighStatusDto();
        var stamps = _data.DepartmentStamps ?? Array.Empty<DepartmentStampDto>();

        return name.Tier switch
        {
            CastNameTier.Common => new CastBadgeResult(
                Title:           modularTitle,                     // null for common per TitleBuilder
                Condition:       Pick(mf.Conditions, rng),
                Note:            Pick(mf.OfficeSins, rng),
                Access:          Pick(mf.Access,     rng),
                Sticker:         null,
                Clearance:       null,
                Legacy:          null,
                Signature:       null,
                DepartmentStamp: null),

            CastNameTier.Uncommon => new CastBadgeResult(
                Title:           modularTitle,
                Condition:       Pick(mf.Conditions, rng),
                Note:            null,
                Access:          Pick(mf.Access,     rng),
                Sticker:         Pick(mf.Stickers,   rng),
                Clearance:       null,
                Legacy:          null,
                Signature:       null,
                DepartmentStamp: null),

            CastNameTier.Rare => new CastBadgeResult(
                Title:           modularTitle,
                Condition:       Pick(mf.Conditions, rng),
                Note:            Pick(mf.OfficeSins, rng),
                Access:          null,
                Sticker:         Pick(mf.Stickers,   rng),
                Clearance:       null,
                Legacy:          null,
                Signature:       null,
                DepartmentStamp: null),

            CastNameTier.Epic => new CastBadgeResult(
                Title:           modularTitle,
                Condition:       null,
                Note:             null,
                Access:          null,
                Sticker:         Pick(mf.Stickers, rng),
                Clearance:       null,
                Legacy:          null,
                Signature:       null,
                DepartmentStamp: FindDeptStamp(modularTitle, stamps) ?? PickStamp(stamps, rng)),

            CastNameTier.Legendary or CastNameTier.Mythic => new CastBadgeResult(
                Title:           modularTitle,
                Condition:       null,
                Note:            null,
                Access:          null,
                Sticker:         null,
                Clearance:       Pick(hf.Clearance, rng),
                Legacy:          Pick(hf.Legacy,    rng),
                Signature:       Pick(hf.Signature, rng),
                DepartmentStamp: FindDeptStamp(modularTitle, stamps) ?? PickStamp(stamps, rng)),

            _ => throw new InvalidOperationException($"Unknown tier {name.Tier}"),
        };
    }

    /// <summary>
    /// Finds the department stamp whose <see cref="DepartmentStampDto.Id"/> appears as a
    /// substring (case-insensitive) of <paramref name="title"/>. Returns null when no
    /// stamp matches OR when title is null/empty. Direct port of JS <c>findDeptStamp()</c>.
    /// </summary>
    public static DepartmentStampDto? FindDeptStamp(string? title, DepartmentStampDto[]? stamps)
    {
        if (string.IsNullOrEmpty(title) || stamps is null) return null;
        var lower = title!.ToLowerInvariant();
        foreach (var s in stamps)
            if (lower.Contains(s.Id, StringComparison.Ordinal)) return s;
        return null;
    }

    private static string? Pick(string[] arr, Random rng)
        => (arr is null || arr.Length == 0) ? null : arr[rng.Next(arr.Length)];

    private static DepartmentStampDto? PickStamp(DepartmentStampDto[] arr, Random rng)
        => (arr is null || arr.Length == 0) ? null : arr[rng.Next(arr.Length)];
}
