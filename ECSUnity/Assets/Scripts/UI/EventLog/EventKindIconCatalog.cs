using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Maps <see cref="ChronicleEventKind"/> to a <see cref="Sprite"/> and a display colour.
/// Used by <see cref="EventLogRow"/> to set the per-row kind icon — WP-3.1.G.
///
/// MOUNTING
/// ────────
/// Create a DefaultEventKindIconCatalog asset via Assets > Create > Warden > EventKindIconCatalog.
/// Assign sprites in the Inspector. If a sprite is missing, a coloured placeholder is used.
///
/// CRT ICON COLOURS (phosphor-green palette with per-kind tints):
///   - SpilledSomething / BrokenItem : #7ab3d4 (blue-grey, minor incident)
///   - PublicArgument                : #e08030 (amber, tension)
///   - PublicHumiliation             : #d04020 (red, high-stress)
///   - AffairRevealed                : #d070a0 (pink, relationship)
///   - Promotion                     : #3CB371 (phosphor green, positive)
///   - Firing                        : #d04020 (red, negative)
///   - KindnessInCrisis              : #3CB371 (green, positive)
///   - Betrayal                      : #c0a020 (gold-amber, moral)
///   - DeathOrLeaving                : #aaaaaa (grey, loss)
///   - Other                         : #8888aa (muted, generic)
/// </summary>
[CreateAssetMenu(
    menuName = "Warden/EventKindIconCatalog",
    fileName = "DefaultEventKindIconCatalog")]
public sealed class EventKindIconCatalog : ScriptableObject
{
    // ── Serialised entries ────────────────────────────────────────────────────

    [System.Serializable]
    public sealed class CatalogEntry
    {
        public ChronicleEventKind Kind;
        public Sprite             Icon;
        public Color              TintColor = Color.white;
    }

    [SerializeField]
    private CatalogEntry[] _entries = new CatalogEntry[]
    {
        new CatalogEntry { Kind = ChronicleEventKind.SpilledSomething,  TintColor = new Color(0.478f, 0.702f, 0.831f) },
        new CatalogEntry { Kind = ChronicleEventKind.BrokenItem,        TintColor = new Color(0.478f, 0.702f, 0.831f) },
        new CatalogEntry { Kind = ChronicleEventKind.PublicArgument,    TintColor = new Color(0.878f, 0.502f, 0.188f) },
        new CatalogEntry { Kind = ChronicleEventKind.PublicHumiliation, TintColor = new Color(0.816f, 0.251f, 0.125f) },
        new CatalogEntry { Kind = ChronicleEventKind.AffairRevealed,    TintColor = new Color(0.816f, 0.439f, 0.627f) },
        new CatalogEntry { Kind = ChronicleEventKind.Promotion,         TintColor = new Color(0.235f, 0.702f, 0.443f) },
        new CatalogEntry { Kind = ChronicleEventKind.Firing,            TintColor = new Color(0.816f, 0.251f, 0.125f) },
        new CatalogEntry { Kind = ChronicleEventKind.KindnessInCrisis,  TintColor = new Color(0.235f, 0.702f, 0.443f) },
        new CatalogEntry { Kind = ChronicleEventKind.Betrayal,          TintColor = new Color(0.753f, 0.627f, 0.125f) },
        new CatalogEntry { Kind = ChronicleEventKind.DeathOrLeaving,    TintColor = new Color(0.667f, 0.667f, 0.667f) },
        new CatalogEntry { Kind = ChronicleEventKind.Other,             TintColor = new Color(0.533f, 0.533f, 0.667f) },
    };

    // ── Lookup ─────────────────────────────────────────────────────────────────

    /// <summary>Returns the sprite for the given kind, or null if unassigned.</summary>
    public Sprite GetIcon(ChronicleEventKind kind)
    {
        foreach (var e in _entries)
            if (e.Kind == kind) return e.Icon;
        return null;
    }

    /// <summary>Returns the tint colour for the given kind.</summary>
    public Color GetTint(ChronicleEventKind kind)
    {
        foreach (var e in _entries)
            if (e.Kind == kind) return e.TintColor;
        return Color.white;
    }
}
