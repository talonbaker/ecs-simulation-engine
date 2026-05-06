using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Reads <see cref="IlluminationDto"/> from <see cref="WorldStateDto.Rooms"/> each frame
/// and updates the floor-quad material tint for every room managed by
/// <see cref="RoomRectangleRenderer"/>.
///
/// DESIGN
/// ───────
/// RoomAmbientTintApplier is a separate MonoBehaviour from RoomRectangleRenderer so that
/// the tint concern can be iterated independently and is easy to disable for debugging.
/// It references RoomRectangleRenderer and reads material handles from it.
///
/// TINT CALCULATION
/// ─────────────────
/// Each room material uses the "ECSUnity/RoomTint" shader which exposes:
///   _Color         — base palette color (set by RoomRectangleRenderer, never changed here)
///   _TintColor     — Kelvin → RGB color of the illumination
///   _TintIntensity — how strongly the Kelvin tint overrides the palette color
///   _Alpha         — transparency (floor quads stay at 1.0; walls are managed by WallFadeController)
///
/// The final visible brightness is driven by the material's _TintIntensity * normalised
/// AmbientLevel, clamped between <see cref="LightingConfig.minimumRoomBrightness"/> and
/// <see cref="LightingConfig.maximumRoomBrightness"/>.
///
/// PERFORMANCE
/// ────────────
/// Material.SetColor / SetFloat are cheap per-frame calls (~30 ns each) for a small number of rooms.
/// At 20–30 rooms the total cost is well under 1 ms. No mesh re-allocation occurs.
///
/// MOUNTING
/// ─────────
/// Add to any GameObject in the scene. Assign _engineHost and _roomRenderer in the Inspector.
/// Alternatively, SceneBootstrapper adds this automatically alongside RoomRectangleRenderer.
/// </summary>
public sealed class RoomAmbientTintApplier : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("Source of WorldState (rooms and their illumination).")]
    private EngineHost _engineHost;

    [SerializeField]
    [Tooltip("Provides per-room material handles via GetRoomMaterial().")]
    private RoomRectangleRenderer _roomRenderer;

    [SerializeField]
    [Tooltip("Lighting tunable parameters.")]
    private LightingConfig _config;

    // ── Material property IDs (cached for speed) ──────────────────────────────

    // Unity's Shader.PropertyToID call hashes string → int once, then the int is used
    // every frame instead of re-hashing the string.
    private static readonly int TintColorId     = Shader.PropertyToID("_TintColor");
    private static readonly int TintIntensityId = Shader.PropertyToID("_TintIntensity");
    private static readonly int AlphaId         = Shader.PropertyToID("_Alpha");

    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_engineHost == null || _roomRenderer == null) return;

        var worldState = _engineHost.WorldState;
        if (worldState?.Rooms == null) return;

        // Use a fallback config so the renderer is never stuck at defaults when the
        // Inspector slot is left unassigned during prototyping.
        float tintBlend      = _config != null ? _config.ambientTintBlend      : 0.28f;
        float minBrightness  = _config != null ? _config.minimumRoomBrightness : 0.18f;
        float maxBrightness  = _config != null ? _config.maximumRoomBrightness : 0.95f;

        foreach (var room in worldState.Rooms)
        {
            var mat = _roomRenderer.GetRoomMaterial(room.Id);
            if (mat == null) continue;   // room not rendered (wrong floor, etc.)

            ApplyIllumination(mat, room.Illumination, tintBlend, minBrightness, maxBrightness);
        }
    }

    // ── Core tint logic ───────────────────────────────────────────────────────

    private static void ApplyIllumination(
        Material       mat,
        IlluminationDto illumination,
        float          tintBlend,
        float          minBrightness,
        float          maxBrightness)
    {
        if (illumination == null)
        {
            // Safety: if the projector didn't populate illumination, fade to minimum.
            mat.SetColor(TintColorId,     Color.white);
            mat.SetFloat(TintIntensityId, minBrightness);
            mat.SetFloat(AlphaId,         1f);
            return;
        }

        // AmbientLevel is 0–100 (integer, engine units).
        // Normalise to 0..1, then remap to [minBrightness, maxBrightness].
        float normalised  = Mathf.Clamp01(illumination.AmbientLevel / 100f);
        float brightness  = Mathf.Lerp(minBrightness, maxBrightness, normalised);

        // Convert color temperature → RGB tint.
        // Then scale the tint by brightness so dim rooms are both darker AND desaturated
        // (a room with AmbientLevel=0 should feel like an unlit space, not just dark+colorful).
        Color kelvinColor = KelvinToRgb.Convert(illumination.ColorTemperatureK);
        Color tintColor   = kelvinColor * brightness;

        // TintIntensity controls how much the kelvin color blends over the base palette.
        // We multiply tintBlend by the brightness so very dim rooms also show less tint
        // (the palette color should show through a bit even in darkness).
        float intensity = tintBlend * brightness;

        mat.SetColor(TintColorId,     tintColor);
        mat.SetFloat(TintIntensityId, intensity);
        mat.SetFloat(AlphaId,         1f);   // floor quads are always fully opaque
    }

    // ── Test / diagnostic accessors ───────────────────────────────────────────

    /// <summary>
    /// Synchronously applies tint for one room by ID. Used by play-mode tests to
    /// drive the tint without waiting for an Update() call.
    /// </summary>
    public void ForceApply(string roomId, IlluminationDto illumination)
    {
        if (_roomRenderer == null) return;
        var mat = _roomRenderer.GetRoomMaterial(roomId);
        if (mat == null) return;

        float tintBlend     = _config != null ? _config.ambientTintBlend      : 0.28f;
        float minBrightness = _config != null ? _config.minimumRoomBrightness : 0.18f;
        float maxBrightness = _config != null ? _config.maximumRoomBrightness : 0.95f;

        ApplyIllumination(mat, illumination, tintBlend, minBrightness, maxBrightness);
    }

    /// <summary>
    /// Returns the _TintColor currently on the room's material. Used by tests.
    /// Returns Color.clear if the room is not found.
    /// </summary>
    public Color GetRoomTintColor(string roomId)
    {
        if (_roomRenderer == null) return Color.clear;
        var mat = _roomRenderer.GetRoomMaterial(roomId);
        return mat != null ? mat.GetColor(TintColorId) : Color.clear;
    }

    /// <summary>
    /// Returns the _TintIntensity currently on the room's material. Used by tests.
    /// Returns -1 if the room is not found.
    /// </summary>
    public float GetRoomTintIntensity(string roomId)
    {
        if (_roomRenderer == null) return -1f;
        var mat = _roomRenderer.GetRoomMaterial(roomId);
        return mat != null ? mat.GetFloat(TintIntensityId) : -1f;
    }
}
