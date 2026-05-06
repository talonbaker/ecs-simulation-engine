namespace APIFramework.Cast;

/// <summary>
/// Per-NPC badge metadata generated alongside (or after) a <see cref="CastNameResult"/>.
/// Pure data — UI consumers (future hire-screen, inspector tab, bulletin board) format
/// the populated fields into the visual badge card. Different tiers populate different
/// subsets per the JS source's <c>generateBadge()</c> rules:
///
/// - Common:    Condition + Note + Access
/// - Uncommon:  Condition + Sticker + Access
/// - Rare:      Condition + Note + Sticker
/// - Epic:      Sticker + DepartmentStamp
/// - Legendary/Mythic: Clearance + Legacy + Signature + DepartmentStamp
///
/// <see cref="Title"/> is independently re-rolled via the modular title builder
/// (rank / domain / function), NOT inherited from the name's title — matches the JS
/// source where <c>generateBadge()</c> calls <c>generateTitle(tier)</c> separately.
/// Common-tier badges may have <c>Title == null</c>; uncommon+ have a tier-appropriate
/// chance of populating it (Uncommon ~15%, Rare ~40%, Epic ~80%, Legendary/Mythic always).
/// </summary>
public sealed record CastBadgeResult(
    string?              Title,
    string?              Condition,
    string?              Note,
    string?              Access,
    string?              Sticker,
    string?              Clearance,
    string?              Legacy,
    string?              Signature,
    DepartmentStampDto?  DepartmentStamp);
