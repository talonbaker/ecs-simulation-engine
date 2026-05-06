using UnityEngine;

/// <summary>
/// ScriptableObject that holds all tunable parameters for the WP-3.1.C lighting layer.
///
/// USAGE
/// ──────
/// Create via: Assets → Create → ECSUnity → LightingConfig.
/// Default values are shipped as <c>Assets/Settings/DefaultLightingConfig.asset</c>.
/// Drag the asset into any MonoBehaviour that exposes a <c>LightingConfig</c> Inspector slot.
///
/// WHY SCRIPTABLEOBJECT
/// ─────────────────────
/// All tunables live here so designers can tweak lighting feel without recompiling.
/// Renderers read from this config every frame (cheap ScriptableObject field read).
///
/// PERFORMANCE NOTE
/// ─────────────────
/// None of these values are read in a hot loop. Reading them in Update on 4 MonoBehaviours
/// costs negligible CPU. If a perf regression appears, cache the struct values on Awake
/// and only re-cache when the asset reference changes.
/// </summary>
[CreateAssetMenu(menuName = "ECSUnity/LightingConfig", fileName = "DefaultLightingConfig")]
public sealed class LightingConfig : ScriptableObject
{
    // ── Ambient tint ──────────────────────────────────────────────────────────

    [Header("Ambient Room Tint")]
    [Tooltip("Fraction of the color-temperature tint blended into the room's base palette color. " +
             "0 = pure palette color; 1 = pure kelvin color. Keep below 0.5 for era-appropriate muted look.")]
    [Range(0f, 1f)]
    public float ambientTintBlend = 0.28f;

    [Tooltip("Minimum brightness multiplier for rooms even when AmbientLevel is 0. " +
             "Prevents completely black rooms (floors still need to be readable).")]
    [Range(0f, 1f)]
    public float minimumRoomBrightness = 0.18f;

    [Tooltip("Maximum brightness multiplier at AmbientLevel = 100. " +
             "Should be <= 1.0 to keep the era-appropriate desaturated look.")]
    [Range(0.5f, 1f)]
    public float maximumRoomBrightness = 0.95f;

    // ── Sun beam ──────────────────────────────────────────────────────────────

    [Header("Sun Beam Overlay")]
    [Tooltip("Maximum alpha of a daytime sun beam at low sun elevation (dawn / dusk). " +
             "Higher = more visible beam slice.")]
    [Range(0f, 0.8f)]
    public float beamMaxAlpha = 0.32f;

    [Tooltip("Minimum sun elevation in degrees before beams start appearing. " +
             "Below this the sun is below the horizon (night) or too low to cast interior beams.")]
    [Range(0f, 15f)]
    public float beamMinElevationDeg = 3f;

    [Tooltip("At this sun elevation (degrees) the beam reaches maximum length. " +
             "Below this angle, beams are at maximum stretch.")]
    [Range(5f, 45f)]
    public float beamMaxLengthElevationDeg = 20f;

    [Tooltip("Maximum world-unit length of a beam quad into the room.")]
    [Range(2f, 20f)]
    public float beamMaxLengthUnits = 12f;

    [Tooltip("Base width multiplier of the beam relative to sqrt(AreaSqTiles).")]
    [Range(0.5f, 4f)]
    public float beamWidthMultiplier = 1.4f;

    [Tooltip("Alpha of the reversed 'interior-light-spills-out' night beam. " +
             "Should be much lower than daytime beam to suggest a lit window, not a searchlight.")]
    [Range(0f, 0.3f)]
    public float beamNightSpillAlpha = 0.12f;

    [Tooltip("Length of the outward night-spill beam in world units.")]
    [Range(1f, 8f)]
    public float beamNightSpillLength = 3f;

    // ── Light source halos ────────────────────────────────────────────────────

    [Header("Light Source Halos")]
    [Tooltip("World-unit radius of the halo quad when Intensity = 100.")]
    [Range(0.5f, 5f)]
    public float haloMaxRadius = 2.5f;

    [Tooltip("Minimum halo alpha (at Intensity = 0). " +
             "Usually 0 — a dead light has no halo.")]
    [Range(0f, 0.3f)]
    public float haloMinAlpha = 0f;

    [Tooltip("Maximum halo alpha (at Intensity = 100).")]
    [Range(0.1f, 1f)]
    public float haloMaxAlpha = 0.55f;

    [Tooltip("Primary frequency of the flickering oscillation in cycles-per-tick. " +
             "Lower = slower flicker; higher = more frantic.")]
    [Range(0.01f, 0.25f)]
    public float flickerFrequency = 0.07f;

    [Tooltip("How much high-frequency per-tick noise is mixed into the flicker. " +
             "0 = smooth sine wave only; 1 = mostly noise.")]
    [Range(0f, 1f)]
    public float flickerNoiseMix = 0.35f;

    [Tooltip("Probability per tick that a 'Dying' source drops to zero intensity. " +
             "Implemented deterministically via seeded hash.")]
    [Range(0f, 0.5f)]
    public float dyingDropProbability = 0.08f;

    [Tooltip("Base intensity fraction applied to Dying sources (before potential zero-drop). " +
             "0.2 = the light is at 20% of nominal intensity most of the time.")]
    [Range(0f, 0.5f)]
    public float dyingBaseIntensityFraction = 0.22f;

    // ── Wall fade ─────────────────────────────────────────────────────────────

    [Header("Wall Fade on Occlusion")]
    [Tooltip("Target alpha for walls that occlude the camera's view of the focus point. " +
             "UX bible §2.1 requires <= 0.4.")]
    [Range(0f, 0.5f)]
    public float wallFadedAlpha = 0.25f;

    [Tooltip("Alpha for walls that do not occlude the focus. Should be 1 (fully opaque).")]
    [Range(0.5f, 1f)]
    public float wallFullAlpha = 1.0f;

    [Tooltip("Seconds to interpolate from full → faded (and back) to avoid pop.")]
    [Range(0.05f, 0.5f)]
    public float wallFadeSeconds = 0.18f;

    [Tooltip("Height of wall quads in world units. Walls taller than the camera pitch need this " +
             "large enough to be visible but not dominate the isometric view.")]
    [Range(0.5f, 6f)]
    public float wallHeight = 2.5f;

    [Tooltip("How far above the floor the wall quads sit. Prevents Z-fighting with the floor mesh.")]
    [Range(0f, 0.2f)]
    public float wallBaseY = 0.02f;

    // ── Day-night directional light ───────────────────────────────────────────

    [Header("Day-Night Directional Light")]
    [Tooltip("Maximum intensity of the directional (sun) light at noon.")]
    [Range(0f, 2f)]
    public float sunMaxIntensity = 1.0f;

    [Tooltip("Minimum directional light intensity at deep night. " +
             "Small non-zero value simulates ambient moonlight.")]
    [Range(0f, 0.3f)]
    public float sunMinIntensity = 0.04f;

    [Tooltip("Pitch angle of the directional light when the sun is at the horizon (elevation = 0). " +
             "Adjust so beams land on the floor at a visually pleasing low angle.")]
    [Range(0f, 30f)]
    public float sunHorizonPitchDeg = 5f;
}
