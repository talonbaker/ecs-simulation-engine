using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that describes the full palette of items the player can place
/// in Build Mode (WP-3.1.D).
///
/// STRUCTURE
/// ──────────
/// The catalog is a flat list of <see cref="PaletteEntry"/> records grouped by
/// <see cref="PaletteCategory"/>. The <see cref="BuildPaletteUI"/> reads this
/// catalog to build its side panel at runtime.
///
/// Categories (per spec and UX bible §3.5):
///   Structural   — walls, doors
///   Furniture    — desks, chairs, conference table
///   Props        — phones, plants, water cooler, etc.
///   NamedAnchor  — unique-instance fixtures (Microwave, Fridge)
///
/// NAMED-ANCHOR UNIQUENESS
/// ────────────────────────
/// Entries with <see cref="PaletteEntry.UniqueInstance"/> = true may only have ONE
/// live copy in the world at a time. If an existing instance is already present,
/// the palette entry shows a "move existing" affordance instead of "place new".
/// <see cref="BuildPaletteUI"/> checks this at render time.
///
/// JSON ROUND-TRIP
/// ────────────────
/// The catalog data also lives in <c>docs/c2-content/build-palette-catalog.json</c>
/// for content-authoring workflows. <see cref="BuildPaletteCatalogJsonTests"/> verifies
/// both sources agree.
/// </summary>
[CreateAssetMenu(menuName = "ECSUnity/Build Palette Catalog", fileName = "DefaultBuildPaletteCatalog")]
public sealed class BuildPaletteCatalog : ScriptableObject
{
    [SerializeField]
    [Tooltip("All palette entries, ordered as they appear in the panel.")]
    public List<PaletteEntry> Entries = new();

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>Returns all entries in a given category.</summary>
    public IEnumerable<PaletteEntry> GetCategory(PaletteCategory category)
    {
        foreach (var e in Entries)
            if (e.Category == category) yield return e;
    }

    /// <summary>Returns the entry with the given template ID, or null if not found.</summary>
    public PaletteEntry? FindByTemplateId(Guid templateId)
    {
        foreach (var e in Entries)
            if (e.TemplateId == templateId) return e;
        return null;
    }

    /// <summary>Returns all unique-instance entries (Microwave, Fridge, etc.).</summary>
    public IEnumerable<PaletteEntry> UniqueEntries()
    {
        foreach (var e in Entries)
            if (e.UniqueInstance) yield return e;
    }
}

// ── Data types ────────────────────────────────────────────────────────────────

/// <summary>A single item in the build palette.</summary>
[Serializable]
public sealed class PaletteEntry
{
    [Tooltip("Human-readable display name shown in the palette panel.")]
    public string Label = string.Empty;

    [Tooltip("Category determines which tab / section this entry appears under.")]
    public PaletteCategory Category;

    [Tooltip("Engine template GUID to pass to IWorldMutationApi.SpawnStructural. " +
             "Must match a template registered in the engine's template registry.")]
    public string TemplateIdString = string.Empty;

    [Tooltip("CRT-style icon sprite shown next to the item name.")]
    public Sprite? Icon;

    [Tooltip("If true, only one instance of this item may exist in the world at a time " +
             "(e.g. the Microwave). The palette shows 'Move' instead of 'Place' when an " +
             "instance already exists.")]
    public bool UniqueInstance;

    [Tooltip("Human-readable tooltip shown when the player hovers over this palette entry.")]
    [TextArea(2, 4)]
    public string Tooltip = string.Empty;

    /// <summary>Parsed TemplateId. Returns Guid.Empty if TemplateIdString is invalid.</summary>
    public Guid TemplateId
    {
        get
        {
            if (Guid.TryParse(TemplateIdString, out Guid id)) return id;
            return Guid.Empty;
        }
    }
}

/// <summary>Top-level categories in the build palette.</summary>
public enum PaletteCategory
{
    /// <summary>Walls and doors — changes floor topology.</summary>
    Structural,

    /// <summary>Desks, chairs, conference tables.</summary>
    Furniture,

    /// <summary>Small interactive props — phones, computers, plants, etc.</summary>
    Props,

    /// <summary>Unique fixtures — Microwave, Fridge. Only one per office.</summary>
    NamedAnchor,
}
