namespace APIFramework.Cast;

/// <summary>
/// Rarity tier of a generated cast name. Drop rates per <c>name-data.json#tierThresholds</c>;
/// defaults: Common 55%, Uncommon 27%, Rare 12%, Epic 4%, Legendary 1.5%, Mythic 0.5%.
/// </summary>
public enum CastNameTier
{
    /// <summary>Vanilla "First StaticLast"; no title.</summary>
    Common,
    /// <summary>"First FusedSurname"; rare title.</summary>
    Uncommon,
    /// <summary>"First SuffixedFusedSurname" (occasional hyphen); ~40% chance of title.</summary>
    Rare,
    /// <summary>"CorporateTitle First ShortSurname" (occasional hyphen); near-always titled.</summary>
    Epic,
    /// <summary>50/50 divine vs hybrid: divine-style root + epic title, or corp + root + short surname.</summary>
    Legendary,
    /// <summary>"CorporateTitle Root, The EpicTitle" — the full apocalypse.</summary>
    Mythic
}
