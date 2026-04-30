using UnityEngine;

/// <summary>
/// ScriptableObject containing all tunable constants for Build Mode (WP-3.1.D).
///
/// DEFAULTS
/// ─────────
/// Reasonable defaults are baked in below. Override by assigning a custom asset in
/// the Inspector or via Assets/Settings/DefaultBuildModeConfig.asset.
///
/// TUNING NOTES
/// ─────────────
/// - overlayAlpha: keep below 0.2 so the world stays readable. 0.1 is subtle; 0.15 is visible.
/// - ghostAlphaValid / ghostAlphaInvalid: ghost must be readable (>= 0.3) but not fully opaque.
/// - snapGridSize: 1.0 = 1 world unit per tile. Do not change unless grid changes.
/// - disruptionStressGain: AcuteLevel points added when player moves a desk while an NPC is sitting.
/// </summary>
[CreateAssetMenu(menuName = "ECSUnity/Build Mode Config", fileName = "DefaultBuildModeConfig")]
public sealed class BuildModeConfig : ScriptableObject
{
    [Header("Overlay")]
    [Tooltip("Alpha of the beige-blue overlay applied while build mode is active.")]
    [Range(0f, 0.5f)]
    public float overlayAlpha = 0.1f;

    [Tooltip("Overlay color (beige-blue per UX bible).")]
    public Color overlayColor = new Color(0.72f, 0.78f, 0.88f, 1f);

    [Header("Ghost Preview")]
    [Tooltip("Alpha of the ghost mesh when placement is valid.")]
    [Range(0f, 1f)]
    public float ghostAlphaValid = 0.55f;

    [Tooltip("Alpha of the ghost mesh when placement is invalid.")]
    [Range(0f, 1f)]
    public float ghostAlphaInvalid = 0.5f;

    [Tooltip("Tint color when ghost placement is valid (white-ish).")]
    public Color ghostColorValid = new Color(1f, 1f, 1f, 1f);

    [Tooltip("Tint color when ghost placement is invalid (red-ish).")]
    public Color ghostColorInvalid = new Color(1f, 0.25f, 0.2f, 1f);

    [Header("Grid Snapping")]
    [Tooltip("World-unit snap increment. 1.0 = one tile.")]
    [Range(0.25f, 4f)]
    public float snapGridSize = 1f;

    [Tooltip("Allowed rotation steps in degrees (90 = 4 cardinal directions).")]
    [Range(1f, 180f)]
    public float rotationStep = 90f;

    [Header("Context Menu")]
    [Tooltip("Pixel radius around right-click that counts as 'on this entity'.")]
    [Range(10f, 60f)]
    public float contextMenuPickRadius = 24f;

    [Header("Disruption")]
    [Tooltip("AcuteLevel points added to an NPC when their desk/chair is moved while occupied.")]
    [Range(0f, 50f)]
    public float disruptionStressGain = 10f;

    [Tooltip("Irritation drive units added to an NPC when their desk/chair is moved during work hours.")]
    [Range(0f, 30f)]
    public float disruptionIrritationGain = 5f;

    [Header("Outline (structural entities)")]
    [Tooltip("Whether to draw outlines around StructuralTag entities when build mode is active.")]
    public bool showStructuralOutlines = true;

    [Tooltip("Color of the structural outline.")]
    public Color structuralOutlineColor = new Color(0.9f, 0.9f, 0.6f, 0.8f);

    [Tooltip("Width of the structural outline in world-units.")]
    [Range(0.01f, 0.2f)]
    public float structuralOutlineWidth = 0.05f;
}
