using UnityEngine;

/// <summary>
/// Per-NPC runtime silhouette: a root <see cref="GameObject"/> that owns four
/// layered <see cref="SpriteRenderer"/> children (body, hair, headwear, item),
/// a <see cref="ChibiEmotionSlot"/> on an "EmotionOverlay" child transform, and
/// an <see cref="Animator"/> component for animation-state driving.
///
/// HIERARCHY (created by <see cref="CreateFor"/> at spawn time)
/// ──────────────────────────────────────────────────────────────
///   NpcSilhouette_{Name}          ← root; this component lives here; Animator here
///   ├─ Body                       ← SpriteRenderer, sortingOrder 0
///   ├─ Hair                       ← SpriteRenderer, sortingOrder 1
///   ├─ Headwear                   ← SpriteRenderer, sortingOrder 2
///   ├─ Item                       ← SpriteRenderer, sortingOrder 3
///   └─ EmotionOverlay             ← ChibiEmotionSlot; head-anchor offset
///
/// LAYER ORDERING
/// ───────────────
/// Sorting order within the same sorting layer:
///   Body=0, Hair=1, Headwear=2, Item=3, EmotionOverlay=10
/// Layers render back-to-front so the item (e.g. coffee mug) appears above clothing.
///
/// TINTING
/// ────────
/// The NPC's dominant color (from SilhouetteComponent.DominantColor) is applied
/// via <see cref="SpriteRenderer.color"/> on the BODY renderer only. This is vertex
/// color, not a material property, so it does NOT break Unity's dynamic batching
/// even when different NPCs carry different tint colors.
/// Hair, headwear, and item renderers are unaffected (authored-color only).
///
/// BILLBOARDING
/// ─────────────
/// NPC silhouettes are 2D sprites on the XZ floor plane. We position the root on
/// the floor (Y = 0) and let the sprites face upward in the default 2D sprite
/// orientation. Camera is expected to be configured for the isometric/oblique view.
/// Unlike the NpcDotRenderer (which had billboard rotation), sprites in the
/// 2D-on-floor configuration are authored facing camera-up — no runtime billboarding
/// needed. If the camera perspective changes, billboard logic can be added here.
///
/// ANIMATION STATE
/// ────────────────
/// <see cref="SetAnimationState"/> sets both the Animator's bool parameters (via
/// <see cref="NpcAnimatorController"/>) and adjusts the ChibiEmotionSlot. The actual
/// sprite-swap between pose variants is handled by <see cref="SetPoseSprites"/> which
/// is called from <see cref="NpcAnimatorController"/> when the Animator enters a new
/// state. At v0.1, only one sprite set exists per NPC (placeholder art), so pose
/// sprites are effectively the same for all states.
///
/// PERFORMANCE
/// ────────────
/// • No per-frame allocations. All child GameObjects and SpriteRenderers are created
///   once in <see cref="CreateFor"/> and reused across frames.
/// • Sprite assignments (SpriteRenderer.sprite) are only written when the silhouette
///   descriptor changes — not every frame.
/// • Animator parameter writes are conditional on state change — not every frame.
/// </summary>
public sealed class NpcSilhouetteInstance : MonoBehaviour
{
    // ── Child renderers (assigned at creation time) ────────────────────────────

    /// <summary>SpriteRenderer for the base body shape. Carries the dominant-color tint.</summary>
    public SpriteRenderer BodyRenderer { get; private set; }

    /// <summary>SpriteRenderer for the hair layer.</summary>
    public SpriteRenderer HairRenderer { get; private set; }

    /// <summary>SpriteRenderer for the headwear layer (invisible when headwear == "none").</summary>
    public SpriteRenderer HeadwearRenderer { get; private set; }

    /// <summary>SpriteRenderer for the distinctive item layer.</summary>
    public SpriteRenderer ItemRenderer { get; private set; }

    /// <summary>Chibi-emotion overlay slot, attached to the EmotionOverlay child transform.</summary>
    public ChibiEmotionSlot EmotionSlot { get; private set; }

    /// <summary>
    /// The Animator on the root GameObject. NpcAnimatorController drives its parameters.
    /// </summary>
    public Animator Animator { get; private set; }

    // ── State ──────────────────────────────────────────────────────────────────

    /// <summary>Current animation / life state driving the animator.</summary>
    public NpcAnimationState CurrentAnimState { get; private set; } = NpcAnimationState.Idle;

    /// <summary>The entity ID string (from WorldStateDto) this instance represents.</summary>
    public string EntityId { get; private set; }

    /// <summary>The last silhouette descriptor applied to this instance.</summary>
    public string LastDominantColor { get; private set; }

    // ── Constants: sort order and child positioning ────────────────────────────

    // World-unit offset from root transform to the EmotionOverlay anchor.
    // Tuned for a 32x64 sprite at 100 PPU (0.64 world-units tall):
    //   head center ≈ 0.48 wu above the root's base.
    private const float EmotionOverlayYOffset = 0.48f;

    private const int SortBody     = 0;
    private const int SortHair     = 1;
    private const int SortHeadwear = 2;
    private const int SortItem     = 3;

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fully-configured NpcSilhouetteInstance under <paramref name="parent"/>.
    ///
    /// Called by <see cref="NpcSilhouetteRenderer"/> once per new NPC entity.
    /// Spawning is the only allocation path — no objects are created per frame.
    /// </summary>
    /// <param name="entityId">Entity ID string for tracking.</param>
    /// <param name="npcName">Display name used to label the root GameObject.</param>
    /// <param name="parent">Parent transform (the NpcSilhouettes root).</param>
    /// <param name="catalog">Catalog supplying sprites and shared materials.</param>
    public static NpcSilhouetteInstance CreateFor(
        string                entityId,
        string                npcName,
        Transform             parent,
        SilhouetteAssetCatalog catalog)
    {
        // Root
        var root = new GameObject($"NpcSilhouette_{npcName}");
        root.transform.SetParent(parent, worldPositionStays: false);

        var inst = root.AddComponent<NpcSilhouetteInstance>();
        inst.EntityId = entityId;

        // Animator on root (RuntimeAnimatorController assigned separately via
        // NpcSilhouetteRenderer._animatorController).
        inst.Animator = root.AddComponent<Animator>();

        // ── Body child ─────────────────────────────────────────────────────────
        inst.BodyRenderer = CreateLayerChild(root.transform, "Body", SortBody,
            catalog != null ? catalog.BodyMaterial : null);

        // ── Hair child ─────────────────────────────────────────────────────────
        inst.HairRenderer = CreateLayerChild(root.transform, "Hair", SortHair,
            catalog != null ? catalog.HairMaterial : null);

        // ── Headwear child ─────────────────────────────────────────────────────
        inst.HeadwearRenderer = CreateLayerChild(root.transform, "Headwear", SortHeadwear,
            catalog != null ? catalog.HeadwearMaterial : null);

        // ── Item child ─────────────────────────────────────────────────────────
        inst.ItemRenderer = CreateLayerChild(root.transform, "Item", SortItem,
            catalog != null ? catalog.ItemMaterial : null);

        // ── EmotionOverlay child (ChibiEmotionSlot stub) ───────────────────────
        var overlayGo = new GameObject("EmotionOverlay");
        overlayGo.transform.SetParent(root.transform, worldPositionStays: false);
        overlayGo.transform.localPosition = new Vector3(0f, EmotionOverlayYOffset, 0f);
        inst.EmotionSlot = overlayGo.AddComponent<ChibiEmotionSlot>();

        return inst;
    }

    // ── Public update methods (called by NpcSilhouetteRenderer each LateUpdate) ─

    /// <summary>
    /// Sets the root transform's world-space position and rotation.
    ///
    /// Engine position (X, Z) is placed on the floor plane (Y = 0).
    /// Facing direction is encoded in FacingComponent.DirectionDeg
    /// (0=north, 90=east, clockwise). We rotate the root's Y axis so silhouette
    /// sprites "face" the right direction in the scene.
    /// </summary>
    /// <param name="worldX">Engine X coordinate.</param>
    /// <param name="worldZ">Engine Z coordinate.</param>
    /// <param name="facingDeg">Facing direction in degrees [0, 360). 0 = north.</param>
    public void UpdatePosition(float worldX, float worldZ, float facingDeg)
    {
        // Y = 0: sprites sit on the floor plane.
        // For a top-down view, the silhouette sprite is rendered upright, so
        // we do not need Y elevation (unlike NpcDotRenderer which used _dotHeight=0.5).
        transform.position = new Vector3(worldX, 0f, worldZ);

        // Facing: rotate root around Y so the sprite "faces" the direction.
        // The sprite's forward is Unity +Z; DirectionDeg=0 is north (engine +Z).
        transform.rotation = Quaternion.Euler(0f, facingDeg, 0f);
    }

    /// <summary>
    /// Applies the silhouette descriptor to the four layer SpriteRenderers.
    ///
    /// This is called once at spawn and again whenever SilhouetteComponent changes
    /// (unusual — it's fixed at spawn time — but guarded for safety).
    ///
    /// Sprite assignment is only written when the values actually change to avoid
    /// redundant GPU uploads.
    /// </summary>
    public void UpdateSilhouette(
        string                 height,
        string                 build,
        string                 hair,
        string                 headwear,
        string                 distinctiveItem,
        string                 dominantColorHex,
        SilhouetteAssetCatalog catalog)
    {
        if (catalog == null) return;

        // Body sprite + tint
        var bodySprite = catalog.GetBodySprite(height, build);
        if (BodyRenderer.sprite != bodySprite)
            BodyRenderer.sprite = bodySprite;

        // Dominant color → body SpriteRenderer.color (vertex tint, doesn't break batching)
        var tint = ParseHexColor(dominantColorHex);
        if (BodyRenderer.color != tint)
        {
            BodyRenderer.color = tint;
            LastDominantColor  = dominantColorHex;
        }

        // Hair sprite
        var hairSprite = catalog.GetHairSprite(hair);
        if (HairRenderer.sprite != hairSprite)
            HairRenderer.sprite = hairSprite;

        // Headwear sprite (null when "none" — renderer stays enabled but invisible via null sprite)
        var headwearSprite = catalog.GetHeadwearSprite(headwear);
        if (HeadwearRenderer.sprite != headwearSprite)
            HeadwearRenderer.sprite = headwearSprite;

        // Enable/disable headwear renderer based on whether there is art
        HeadwearRenderer.enabled = headwearSprite != null;

        // Item sprite
        var itemSprite = catalog.GetItemSprite(distinctiveItem);
        if (ItemRenderer.sprite != itemSprite)
            ItemRenderer.sprite = itemSprite;

        ItemRenderer.enabled = itemSprite != null;
    }

    /// <summary>
    /// Sets the animation state on this instance. Stores the new state for
    /// <see cref="NpcAnimatorController"/> to read each frame.
    ///
    /// Also drives the ChibiEmotionSlot for states that have a well-known overlay:
    ///   Sleep → SleepZ shown
    ///   Panic → overlay hidden (panic reads through the silhouette pose, not chibi)
    ///   Dead  → all overlays hidden; animator frozen
    /// </summary>
    public void SetAnimationState(NpcAnimationState state)
    {
        if (CurrentAnimState == state) return;

        CurrentAnimState = state;

        // Drive chibi overlay for states that have it.
        switch (state)
        {
            case NpcAnimationState.Sleep:
                // Sleep-Z chibi icon: slot exists even if no sprite is loaded yet.
                // This satisfies AT-06 / AnimatorSleepStateTests.
                EmotionSlot.Show(IconKind.SleepZ);
                break;

            case NpcAnimationState.Dead:
                EmotionSlot.Hide();
                // Dead NPCs do not animate. NpcAnimatorController disables Animator.
                break;

            case NpcAnimationState.Idle:
            case NpcAnimationState.Walk:
            case NpcAnimationState.Sit:
            case NpcAnimationState.Talk:
            case NpcAnimationState.Panic:
                // Clear any previously-shown chibi icon when switching away from sleep.
                // For Panic, the fear reads through the frozen silhouette pose, not chibi.
                EmotionSlot.Hide();
                break;
        }
    }

    // ── Layer child factory helper ─────────────────────────────────────────────

    private static SpriteRenderer CreateLayerChild(
        Transform parent,
        string    name,
        int       sortingOrder,
        Material  sharedMaterial)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, worldPositionStays: false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;

        // Assign the shared material for batching.
        // If no material is available yet (e.g. Inspector not wired), Unity will use
        // the default Sprites/Default material automatically.
        if (sharedMaterial != null)
            sr.sharedMaterial = sharedMaterial;

        return sr;
    }

    // ── Color parsing ──────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a CSS hex color string to a Unity Color.
    /// Supports "#RRGGBB" and "#RGB" forms. Returns white on parse failure.
    /// Uses System.Convert.ToInt32(string, 16) which is available in all
    /// Unity-supported .NET profiles.
    /// </summary>
    public static Color ParseHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Color.white;

        // Strip leading '#'
        string raw = hex.TrimStart('#');

        // Expand 3-digit shorthand to 6-digit
        if (raw.Length == 3)
            raw = $"{raw[0]}{raw[0]}{raw[1]}{raw[1]}{raw[2]}{raw[2]}";

        if (raw.Length != 6) return Color.white;

        try
        {
            int rgb = System.Convert.ToInt32(raw, 16);
            float r = ((rgb >> 16) & 0xFF) / 255f;
            float g = ((rgb >> 8)  & 0xFF) / 255f;
            float b = ( rgb        & 0xFF) / 255f;
            return new Color(r, g, b, 1f);
        }
        catch
        {
            return Color.white;
        }
    }
}

/// <summary>
/// Animation states that map engine simulation state to Unity Animator states.
/// Consumed by <see cref="NpcAnimatorController"/> which sets corresponding
/// Animator bool parameters.
/// </summary>
public enum NpcAnimationState
{
    /// <summary>No intent; standing still. Default state.</summary>
    Idle,

    /// <summary>Moving toward a target (IntendedActionKind.Approach, velocity > 0).</summary>
    Walk,

    /// <summary>Seated at a desk or other anchor (Activity == AtDesk or similar).</summary>
    Sit,

    /// <summary>In active dialogue (IntendedActionKind.Dialog).</summary>
    Talk,

    /// <summary>Choking OR MoodComponent.PanicLevel >= 0.5. Frozen, facing forward.</summary>
    Panic,

    /// <summary>LifeState == Incapacitated (fainting) or scheduled sleep.</summary>
    Sleep,

    /// <summary>LifeState == Deceased. Static slumped pose; no animation.</summary>
    Dead,
}
