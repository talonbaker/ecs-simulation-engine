using UnityEngine;

/// <summary>
/// Stub overlay slot for chibi-emotion icons that appear above an NPC's head.
///
/// DESIGN (UX-UI bible §3.8 / WP-3.1.B)
/// ───────────────────────────────────────
/// Each <see cref="NpcSilhouetteInstance"/> has one ChibiEmotionSlot attached to
/// the "EmotionOverlay" child Transform, positioned at the NPC's head anchor.
///
/// This packet (WP-3.1.B) ships only the slot — the child Transform, the visibility
/// toggle, and the <see cref="Show"/>/<see cref="Hide"/> API. No icon sprites are
/// loaded or rendered yet. WP-3.1.E (player UI) will inject actual sprites.
///
/// API CONTRACT
/// ─────────────
/// • <see cref="Show(IconKind)"/> — sets the active icon. If no sprite is loaded for
///   that kind, the slot silently stays invisible. Never throws.
/// • <see cref="Hide"/> — hides any visible icon. Never throws.
/// • <see cref="CurrentIcon"/> — read-only; what the slot is currently showing (or None).
///
/// SPRITE WIRING (future — WP-3.1.E)
/// ────────────────────────────────────
/// When WP-3.1.E ships:
/// 1. Add [SerializeField] Sprite[] _iconSprites (indexed by IconKind ordinal).
/// 2. Populate in the prefab or via ScriptableObject.
/// 3. The Show() body here already contains the sprite-assignment call — it is
///    guarded by a null check so it compiles and runs safely at v0.1.
///
/// PERFORMANCE
/// ────────────
/// ChibiEmotionSlot uses a single SpriteRenderer on its own GameObject. When hidden
/// (`_spriteRenderer.enabled = false`) it contributes zero draw calls. Showing it
/// adds one draw call shared with other visible chibi overlays if they use the same
/// sprite atlas material (future work; atlas is not yet created at v0.1).
/// </summary>
[DisallowMultipleComponent]
public sealed class ChibiEmotionSlot : MonoBehaviour
{
    // ── Inspector (populated by WP-3.1.E) ────────────────────────────────────

    [SerializeField]
    [Tooltip("Sprites indexed by IconKind ordinal. Leave empty until WP-3.1.E ships art.")]
    private Sprite[] _iconSprites = System.Array.Empty<Sprite>();

    // ── Runtime state ─────────────────────────────────────────────────────────

    private SpriteRenderer _spriteRenderer;

    /// <summary>The icon currently displayed, or <see cref="IconKind.None"/> if hidden.</summary>
    public IconKind CurrentIcon { get; private set; } = IconKind.None;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Ensure there is exactly one SpriteRenderer child for the icon sprite.
        // If the prefab already has one, use it; otherwise create it at runtime.
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null)
            _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        // Sort order: chibi overlays appear above all four silhouette layers.
        // Silhouette layers use sort order 0–3 (body, hair, headwear, item).
        // Chibi sits above them all at order 10.
        _spriteRenderer.sortingOrder = 10;

        // Hide by default — nothing to show yet.
        _spriteRenderer.enabled = false;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Show the specified icon above this NPC's head.
    ///
    /// If <paramref name="kind"/> is <see cref="IconKind.None"/>, this is equivalent
    /// to calling <see cref="Hide"/>.
    ///
    /// If no sprite has been loaded for <paramref name="kind"/> (e.g. at v0.1 before
    /// art is authored), the call succeeds silently — the slot stays invisible.
    /// This is by design: the slot API must be callable from animator/state code
    /// without crashing even when no sprite atlas is present.
    /// </summary>
    /// <param name="kind">The icon to display.</param>
    public void Show(IconKind kind)
    {
        if (kind == IconKind.None)
        {
            Hide();
            return;
        }

        CurrentIcon = kind;

        int index = (int)kind;

        // Assign sprite if available; stay invisible if not (v0.1 stub path).
        if (_iconSprites != null && index < _iconSprites.Length && _iconSprites[index] != null)
        {
            _spriteRenderer.sprite  = _iconSprites[index];
            _spriteRenderer.enabled = true;
        }
        else
        {
            // No sprite loaded yet — record the intent but don't crash.
            // The slot remains invisible until art is wired in WP-3.1.E.
            _spriteRenderer.enabled = false;
        }
    }

    /// <summary>
    /// Hide any currently-displayed icon.
    /// Always safe to call; never throws.
    /// </summary>
    public void Hide()
    {
        CurrentIcon             = IconKind.None;
        _spriteRenderer.enabled = false;
    }

    // ── Test accessors ────────────────────────────────────────────────────────

    /// <summary>True if the slot's SpriteRenderer is currently enabled.</summary>
    public bool IsVisible => _spriteRenderer != null && _spriteRenderer.enabled;
}
