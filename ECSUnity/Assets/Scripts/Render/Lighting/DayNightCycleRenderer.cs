using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Drives a Unity <see cref="Light"/> (directional) to match the engine's sun state
/// from <see cref="WorldStateDto.Clock.Sun"/> each render frame.
///
/// WHAT IT CONTROLS
/// ─────────────────
/// 1. Rotation — directional light yaw (azimuth) and pitch (elevation).
///    Low elevation at dawn/dusk → shallow angle; high at noon → overhead.
///    Elevation below 0 (night) → light points straight down at minimum intensity
///    to simulate moonlight/ambient night.
///
/// 2. Color — maps sun elevation and DayPhase to the classic Kelvin palette:
///    - Night         →  cool deep blue  (~8 500 K), very dim
///    - Early morning →  warm deep orange (~2 000 K), low intensity
///    - Mid-morning   →  warm yellow-white (~4 000 K)
///    - Afternoon     →  neutral white    (~5 500 K), full intensity
///    - Evening       →  warm orange-red  (~3 000 K)
///    - Dusk          →  deep warm orange (~2 200 K), fading fast
///
/// 3. Intensity — scales linearly with a soft version of sin(elevation),
///    clamped between LightingConfig.sunMinIntensity and sunMaxIntensity.
///
/// AZIMUTH CONVENTION
/// ───────────────────
/// SunStateDto.AzimuthDeg: 0 = North, 90 = East, 180 = South, 270 = West.
/// Unity directional light rotation: the light "comes from" the direction it points.
/// We map sun azimuth → light yaw such that the sun at 90° (east) casts eastward-to-west
/// rays (the light points west).
///
/// MOUNTING
/// ─────────
/// Attach to any GameObject. Drag the scene's Directional Light into _directionalLight.
/// Drag EngineHost and optionally LightingConfig.
/// </summary>
public sealed class DayNightCycleRenderer : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("The scene's directional (sun) light. Must be a Directional light type.")]
    private Light _directionalLight;

    [SerializeField]
    [Tooltip("Source of WorldState (clock / sun state).")]
    private EngineHost _engineHost;

    [SerializeField]
    [Tooltip("Lighting tunable parameters.")]
    private LightingConfig _config;

    // ── Runtime state ─────────────────────────────────────────────────────────

    // Last-applied values — cached so we can expose them to tests without
    // triggering another Update() call.
    private Color _lastLightColor     = Color.white;
    private float _lastLightIntensity = 1f;
    private float _lastAzimuthDeg     = 180f;
    private float _lastElevationDeg   = 45f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_directionalLight == null || _engineHost == null) return;

        var ws  = _engineHost.WorldState;
        var sun = ws?.Clock?.Sun;

        float sunMax = _config != null ? _config.sunMaxIntensity  : 1.0f;
        float sunMin = _config != null ? _config.sunMinIntensity  : 0.04f;

        if (sun == null)
        {
            // No sun state available — use a neutral default (afternoon white).
            _directionalLight.color     = Color.white;
            _directionalLight.intensity = sunMax * 0.6f;
            return;
        }

        float azimuth   = (float)sun.AzimuthDeg;
        float elevation = (float)sun.ElevationDeg;

        // ── Rotation ─────────────────────────────────────────────────────────
        // Unity directional light: rotation.eulerAngles.y = yaw (azimuth),
        // rotation.eulerAngles.x = pitch (negative elevation = points down).
        // A directional light at (pitch=−elevation, yaw=azimuth) sends rays in the
        // direction the light transform faces, which is (from sky toward ground) when
        // pitch is negative.
        // We clamp elevation to a minimum visible pitch so the light never points exactly
        // horizontal (avoids divide-by-near-zero in some shadow algorithms).
        float effectiveElevation = Mathf.Max(elevation, _config != null ? _config.sunHorizonPitchDeg : 5f);
        if (elevation < 0f)
            effectiveElevation = 5f;   // night: keep light pointing slightly down for moonlight

        // Yaw: sun at azimuth 180° (south) → light yaw = 180° in Unity.
        _directionalLight.transform.rotation =
            Quaternion.Euler(-effectiveElevation, azimuth, 0f);

        // ── Color ─────────────────────────────────────────────────────────────
        Color lightColor = DayPhaseColor(sun.DayPhase, elevation);

        // ── Intensity ─────────────────────────────────────────────────────────
        // sin(elevation) gives a smooth arc; clamp so night is always at minimum.
        float sinElev   = Mathf.Sin(elevation * Mathf.Deg2Rad);
        float intensity = (elevation > 0f)
            ? Mathf.Lerp(sunMin, sunMax, Mathf.Clamp01(sinElev))
            : sunMin;

        _directionalLight.color     = lightColor;
        _directionalLight.intensity = intensity;

        // Cache for test accessors.
        _lastLightColor     = lightColor;
        _lastLightIntensity = intensity;
        _lastAzimuthDeg     = azimuth;
        _lastElevationDeg   = elevation;
    }

    // ── Color temperature by day phase ────────────────────────────────────────

    /// <summary>
    /// Maps the engine's <see cref="DayPhase"/> and elevation to a directional light color.
    /// Colors are sourced from the aesthetic bible §"Time of day" palette commitments.
    /// Kelvin values are approximate artistic targets, not physically exact.
    /// </summary>
    private static Color DayPhaseColor(DayPhase phase, float elevationDeg)
    {
        switch (phase)
        {
            case DayPhase.Night:
                // Cool deep blue — moonlight + ambient night sky.
                return KelvinToRgb.Convert(8500f) * 0.9f;

            case DayPhase.EarlyMorning:
                // Deep warm orange / red — sun just breaking the horizon.
                // Lerp from night-blue to dawn-orange based on elevation.
                float earlyT = Mathf.Clamp01(elevationDeg / 8f);
                return Color.Lerp(
                    KelvinToRgb.Convert(8500f),
                    KelvinToRgb.Convert(2000f),
                    earlyT);

            case DayPhase.MidMorning:
                // Warm yellow climbing toward neutral — elevation 15–35°.
                float midMornT = Mathf.Clamp01((elevationDeg - 8f) / 30f);
                return Color.Lerp(
                    KelvinToRgb.Convert(2800f),
                    KelvinToRgb.Convert(4500f),
                    midMornT);

            case DayPhase.Afternoon:
                // Neutral white — sun high overhead.
                return KelvinToRgb.Convert(5500f);

            case DayPhase.Evening:
                // Warm orange descending — mirrors mid-morning but reversed.
                float evenT = Mathf.Clamp01(1f - (elevationDeg / 40f));
                return Color.Lerp(
                    KelvinToRgb.Convert(5500f),
                    KelvinToRgb.Convert(3000f),
                    evenT);

            case DayPhase.Dusk:
                // Deep warm orange fading to night blue.
                float duskT = Mathf.Clamp01(1f - (elevationDeg / 8f));
                return Color.Lerp(
                    KelvinToRgb.Convert(2200f),
                    KelvinToRgb.Convert(8500f),
                    duskT);

            default:
                return Color.white;
        }
    }

    // ── Test / diagnostic accessors ───────────────────────────────────────────

    /// <summary>Last color applied to the directional light. Exposed for tests.</summary>
    public Color LastLightColor => _lastLightColor;

    /// <summary>Last intensity applied to the directional light. Exposed for tests.</summary>
    public float LastLightIntensity => _lastLightIntensity;

    /// <summary>Last azimuth used. Exposed for tests.</summary>
    public float LastAzimuthDeg => _lastAzimuthDeg;

    /// <summary>Last sun elevation used. Exposed for tests.</summary>
    public float LastElevationDeg => _lastElevationDeg;

    /// <summary>
    /// Directly drives the light from a supplied sun state, bypassing EngineHost.
    /// Used by tests to inject known sun states without needing a full engine boot.
    /// </summary>
    public void ForceApplySunState(float azimuthDeg, float elevationDeg, DayPhase phase)
    {
        if (_directionalLight == null) return;

        float sunMax = _config != null ? _config.sunMaxIntensity : 1.0f;
        float sunMin = _config != null ? _config.sunMinIntensity : 0.04f;

        float effectiveElevation = Mathf.Max(elevationDeg,
            _config != null ? _config.sunHorizonPitchDeg : 5f);
        if (elevationDeg < 0f) effectiveElevation = 5f;

        _directionalLight.transform.rotation =
            Quaternion.Euler(-effectiveElevation, azimuthDeg, 0f);

        Color c = DayPhaseColor(phase, elevationDeg);
        float sinElev   = Mathf.Sin(elevationDeg * Mathf.Deg2Rad);
        float intensity = (elevationDeg > 0f)
            ? Mathf.Lerp(sunMin, sunMax, Mathf.Clamp01(sinElev))
            : sunMin;

        _directionalLight.color     = c;
        _directionalLight.intensity = intensity;

        _lastLightColor     = c;
        _lastLightIntensity = intensity;
        _lastAzimuthDeg     = azimuthDeg;
        _lastElevationDeg   = elevationDeg;
    }
}
