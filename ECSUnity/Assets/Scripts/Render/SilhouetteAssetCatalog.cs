using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that maps silhouette descriptor fields to sprite assets and
/// shared render materials for each silhouette layer.
///
/// DESIGN
/// ───────
/// A silhouette is composed of up to four layered sprites:
///   1. Body   — height × build  (e.g. "short" × "stocky" → body_short_stocky.png)
///   2. Hair   — hair descriptor  (e.g. "medium" → hair_medium.png)
///   3. Headwear — headwear kind   (e.g. "cap" → headwear_cap.png; "none" = null)
///   4. Item   — item kind         (e.g. "coffee-mug" → item_coffee-mug.png)
///
/// The catalog exposes four lookup methods, one per layer. Each returns the
/// matching Sprite from its serialized entry list, or null when no match is found
/// (treated as "no sprite for this layer" by <see cref="NpcSilhouetteInstance"/>).
///
/// SHARED MATERIALS (BATCHING)
/// ─────────────────────────────
/// To keep draw calls bounded (≤ ~10 for 30 NPCs), all SpriteRenderers on the same
/// layer share a single Material instance:
///   - _bodyMaterial   : shared across all NPC body SpriteRenderers
///   - _hairMaterial   : shared across all NPC hair SpriteRenderers
///   - _headwearMaterial: shared across all NPC headwear SpriteRenderers
///   - _itemMaterial   : shared across all NPC item SpriteRenderers
///
/// IMPORTANT: For full dynamic batching the sprites on the same layer must share
/// the same Texture2D (i.e. be packed into a single Sprite Atlas). At v0.1 with
/// placeholder sprites this is enforced by the Python sprite-gen script which
/// generates all sprites from the same source texture. When final art arrives,
/// pack all sprites into a Unity Sprite Atlas and reassign references here.
///
/// Per-NPC dominant-color tint is applied via SpriteRenderer.color on the BODY
/// layer only. SpriteRenderer.color is encoded as vertex color in the sprite mesh,
/// NOT as a material property, so it does NOT break Unity's dynamic batching.
/// Hair, headwear, and item layers are unaffected by the tint (authored colors only).
///
/// USAGE
/// ──────
/// Drag Assets/Settings/SilhouetteAssetCatalog.asset onto the
/// NpcSilhouetteRenderer inspector field. NpcSilhouetteRenderer passes it to each
/// NpcSilhouetteInstance after spawn.
/// </summary>
[CreateAssetMenu(
    fileName = "SilhouetteAssetCatalog",
    menuName  = "ECS/Silhouette Asset Catalog",
    order     = 50)]
public sealed class SilhouetteAssetCatalog : ScriptableObject
{
    // ── Layer materials (assign in Inspector; defaults to Sprites/Default) ────

    [Header("Shared Materials — one per layer for draw-call batching")]

    [SerializeField]
    [Tooltip("Material shared across ALL NPC body SpriteRenderers. " +
             "Must be the same Material instance for dynamic batching. " +
             "Sprites/Default works at v0.1; upgrade to a GPU-instanced material if draw call count exceeds the gate.")]
    private Material _bodyMaterial;

    [SerializeField]
    [Tooltip("Material shared across ALL NPC hair SpriteRenderers.")]
    private Material _hairMaterial;

    [SerializeField]
    [Tooltip("Material shared across ALL NPC headwear SpriteRenderers.")]
    private Material _headwearMaterial;

    [SerializeField]
    [Tooltip("Material shared across ALL NPC item SpriteRenderers.")]
    private Material _itemMaterial;

    // ── Sprite entry arrays ───────────────────────────────────────────────────

    [Header("Body sprites — keyed by height × build (e.g. 'short' × 'stocky')")]
    [SerializeField]
    private BodySpriteEntry[] _bodySprites = Array.Empty<BodySpriteEntry>();

    [Header("Hair sprites — keyed by hair descriptor string from SilhouetteComponent.Hair")]
    [SerializeField]
    private HairSpriteEntry[] _hairSprites = Array.Empty<HairSpriteEntry>();

    [Header("Headwear sprites — keyed by SilhouetteComponent.Headwear (skip 'none')")]
    [SerializeField]
    private HeadwearSpriteEntry[] _headwearSprites = Array.Empty<HeadwearSpriteEntry>();

    [Header("Item sprites — keyed by SilhouetteComponent.DistinctiveItem")]
    [SerializeField]
    private ItemSpriteEntry[] _itemSprites = Array.Empty<ItemSpriteEntry>();

    // ── Runtime lookup caches (built lazily on first use) ─────────────────────

    private Dictionary<string, Sprite>            _bodyCache;
    private Dictionary<string, Sprite>            _hairCache;
    private Dictionary<string, Sprite>            _headwearCache;
    private Dictionary<string, Sprite>            _itemCache;

    // ─────────────────────────────────────────────────────────────────────────

    // ── Material accessors ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the shared body material. Falls back to the built-in Sprites/Default
    /// shader if no material was assigned in the Inspector (safe for v0.1 tests).
    /// </summary>
    public Material BodyMaterial     => _bodyMaterial     ? _bodyMaterial     : FallbackMaterial();

    /// <summary>Returns the shared hair material.</summary>
    public Material HairMaterial     => _hairMaterial     ? _hairMaterial     : FallbackMaterial();

    /// <summary>Returns the shared headwear material.</summary>
    public Material HeadwearMaterial => _headwearMaterial ? _headwearMaterial : FallbackMaterial();

    /// <summary>Returns the shared item material.</summary>
    public Material ItemMaterial     => _itemMaterial     ? _itemMaterial     : FallbackMaterial();

    // ── Sprite lookups ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the body sprite for the given height and build combination, or null
    /// when no matching sprite has been assigned (e.g. at v0.1 with partial art).
    ///
    /// The lookup key is "<paramref name="height"/>/<paramref name="build"/>"
    /// (case-insensitive). Keys are normalised by trimming whitespace.
    /// </summary>
    public Sprite GetBodySprite(string height, string build)
    {
        EnsureBodyCache();
        var key = MakeKey(height, build);
        _bodyCache.TryGetValue(key, out var sprite);
        return sprite;
    }

    /// <summary>
    /// Returns the hair sprite for the given hair descriptor string (from
    /// <c>SilhouetteComponent.Hair</c>), or null when no match exists.
    /// </summary>
    public Sprite GetHairSprite(string hair)
    {
        EnsureHairCache();
        if (string.IsNullOrWhiteSpace(hair)) return null;
        _hairCache.TryGetValue(NormaliseKey(hair), out var sprite);
        return sprite;
    }

    /// <summary>
    /// Returns the headwear sprite for the given headwear kind, or null for
    /// "none" / no match. The "none" value is a valid no-sprite sentinel.
    /// </summary>
    public Sprite GetHeadwearSprite(string headwear)
    {
        if (string.IsNullOrWhiteSpace(headwear)) return null;
        // "none" is the canonical "no headwear" value — no sprite needed.
        if (string.Equals(headwear, "none", StringComparison.OrdinalIgnoreCase)) return null;

        EnsureHeadwearCache();
        _headwearCache.TryGetValue(NormaliseKey(headwear), out var sprite);
        return sprite;
    }

    /// <summary>
    /// Returns the item sprite for the given item kind, or null for "none" / no match.
    /// </summary>
    public Sprite GetItemSprite(string item)
    {
        if (string.IsNullOrWhiteSpace(item)) return null;
        if (string.Equals(item, "none", StringComparison.OrdinalIgnoreCase)) return null;

        EnsureItemCache();
        _itemCache.TryGetValue(NormaliseKey(item), out var sprite);
        return sprite;
    }

    // ── Cache initialisation ──────────────────────────────────────────────────

    private void EnsureBodyCache()
    {
        if (_bodyCache != null) return;
        _bodyCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in _bodySprites)
            if (e != null && !string.IsNullOrWhiteSpace(e.Height) && !string.IsNullOrWhiteSpace(e.Build))
                _bodyCache[MakeKey(e.Height, e.Build)] = e.Sprite;
    }

    private void EnsureHairCache()
    {
        if (_hairCache != null) return;
        _hairCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in _hairSprites)
            if (e != null && !string.IsNullOrWhiteSpace(e.Hair))
                _hairCache[NormaliseKey(e.Hair)] = e.Sprite;
    }

    private void EnsureHeadwearCache()
    {
        if (_headwearCache != null) return;
        _headwearCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in _headwearSprites)
            if (e != null && !string.IsNullOrWhiteSpace(e.Kind))
                _headwearCache[NormaliseKey(e.Kind)] = e.Sprite;
    }

    private void EnsureItemCache()
    {
        if (_itemCache != null) return;
        _itemCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in _itemSprites)
            if (e != null && !string.IsNullOrWhiteSpace(e.Kind))
                _itemCache[NormaliseKey(e.Kind)] = e.Sprite;
    }

    // Called by Unity when the asset is modified in the Editor.
    private void OnValidate()
    {
        // Invalidate caches so changes in the Inspector take effect immediately.
        _bodyCache     = null;
        _hairCache     = null;
        _headwearCache = null;
        _itemCache     = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string MakeKey(string a, string b)
        => $"{NormaliseKey(a)}/{NormaliseKey(b)}";

    private static string NormaliseKey(string s)
        => s?.Trim().ToLowerInvariant() ?? string.Empty;

    private Material _fallbackMaterial;

    private Material FallbackMaterial()
    {
        // Cache the fallback to avoid creating a new Material instance every call.
        if (_fallbackMaterial != null) return _fallbackMaterial;
        _fallbackMaterial = new Material(Shader.Find("Sprites/Default"));
        return _fallbackMaterial;
    }
}

// ── Serializable entry types ──────────────────────────────────────────────────

/// <summary>
/// Inspector entry mapping a height + build combination to a body sprite.
///
/// Example: Height="short", Build="stocky" → body_short_stocky.png placeholder.
/// </summary>
[Serializable]
public sealed class BodySpriteEntry
{
    [Tooltip("Height string from SilhouetteComponent: 'short', 'average', or 'tall'.")]
    public string Height;

    [Tooltip("Build string from SilhouetteComponent: 'slight', 'average', or 'stocky'.")]
    public string Build;

    [Tooltip("Body sprite for this height × build combination. Placeholder at v0.1.")]
    public Sprite Sprite;
}

/// <summary>
/// Inspector entry mapping a hair descriptor string to a hair sprite.
///
/// The key matches <c>SilhouetteComponent.Hair</c> exactly (case-insensitive).
/// Supported values at v0.1: "bald", "short", "medium", "long", "distinctive".
/// Hair color is not yet modelled separately in the component schema.
/// </summary>
[Serializable]
public sealed class HairSpriteEntry
{
    [Tooltip("Hair value from SilhouetteComponent.Hair (e.g. 'short', 'medium', 'long').")]
    public string Hair;

    [Tooltip("Hair sprite for this descriptor. Placeholder at v0.1.")]
    public Sprite Sprite;
}

/// <summary>
/// Inspector entry mapping a headwear kind string to a headwear sprite.
///
/// The key matches <c>SilhouetteComponent.Headwear</c> (case-insensitive).
/// "none" entries are never stored here; <see cref="SilhouetteAssetCatalog.GetHeadwearSprite"/>
/// short-circuits to null before touching the cache.
/// </summary>
[Serializable]
public sealed class HeadwearSpriteEntry
{
    [Tooltip("Headwear kind from SilhouetteComponent.Headwear (e.g. 'cap', 'hat', 'glasses').")]
    public string Kind;

    [Tooltip("Headwear sprite for this kind. Placeholder at v0.1.")]
    public Sprite Sprite;
}

/// <summary>
/// Inspector entry mapping a distinctive-item kind string to an item sprite.
///
/// The key matches <c>SilhouetteComponent.DistinctiveItem</c> (case-insensitive).
/// </summary>
[Serializable]
public sealed class ItemSpriteEntry
{
    [Tooltip("Item kind from SilhouetteComponent.DistinctiveItem (e.g. 'coffee-mug', 'lanyard').")]
    public string Kind;

    [Tooltip("Item sprite for this kind. Placeholder at v0.1.")]
    public Sprite Sprite;
}
