using UnityEngine;

/// <summary>
/// Converts a color temperature in Kelvin to a Unity <see cref="Color"/> (RGB, no alpha).
///
/// ALGORITHM
/// ──────────
/// Uses Mitchell Charity's piecewise approximation ("How to Convert Between Color Temperatures
/// and Blackbody Radiation", widely used in game engines and image-processing tools).
/// Valid range: 1 000 K – 40 000 K.  Values outside this range are clamped to the nearest
/// bound's output; no exception is thrown.
///
/// CALIBRATION POINTS (approximate)
/// ──────────────────────────────────
///   1 000 K  → deep orange-red (candle)
///   2 700 K  → warm incandescent yellow-white
///   4 000 K  → warm fluorescent yellow-green (office fluorescent, cool white)
///   5 500 K  → daylight neutral white
///   6 500 K  → overcast sky, camera standard D65
///  10 000 K  → blue sky
///
/// USAGE IN THIS CODEBASE
/// ───────────────────────
/// Called by <see cref="RoomAmbientTintApplier"/> to map
/// <c>IlluminationDto.ColorTemperatureK</c> → room tint.
/// Called by <see cref="DayNightCycleRenderer"/> to map sun state → directional light color.
/// Called by <see cref="LightSourceHaloRenderer"/> to tint halo quads per fixture type.
/// </summary>
public static class KelvinToRgb
{
    /// <summary>
    /// Converts <paramref name="kelvin"/> to an RGB <see cref="Color"/> (alpha = 1).
    /// Input is clamped to [1 000, 40 000] K before conversion.
    /// </summary>
    public static Color Convert(float kelvin)
    {
        // Clamp to valid range.
        float k = Mathf.Clamp(kelvin, 1000f, 40000f);

        // The algorithm works with k / 100.
        float t = k / 100f;

        float r, g, b;

        // ── Red channel ───────────────────────────────────────────────────────
        // Below 6 600 K: red is at full saturation.
        // Above 6 600 K: red decreases toward blue sky.
        if (t <= 66f)
        {
            r = 255f;
        }
        else
        {
            r = 329.698727446f * Mathf.Pow(t - 60f, -0.1332047592f);
            r = Mathf.Clamp(r, 0f, 255f);
        }

        // ── Green channel ─────────────────────────────────────────────────────
        // Warm tones: logarithmic; cool tones: power falloff.
        if (t <= 66f)
        {
            // Logarithmic fit for the warm range.
            g = 99.4708025861f * Mathf.Log(t) - 161.1195681661f;
            g = Mathf.Clamp(g, 0f, 255f);
        }
        else
        {
            g = 288.1221695283f * Mathf.Pow(t - 60f, -0.0755148492f);
            g = Mathf.Clamp(g, 0f, 255f);
        }

        // ── Blue channel ──────────────────────────────────────────────────────
        // Below ~1 900 K: no blue component.
        // At or above 6 600 K: full blue (daylight / sky).
        // In between: logarithmic ramp.
        if (t >= 66f)
        {
            b = 255f;
        }
        else if (t <= 19f)
        {
            b = 0f;
        }
        else
        {
            b = 138.5177312231f * Mathf.Log(t - 10f) - 305.0447927307f;
            b = Mathf.Clamp(b, 0f, 255f);
        }

        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }

    // ── Pre-computed constants for the most common office fixtures ────────────
    // These save the per-frame conversion for fixed-temperature sources.

    /// <summary>Warm incandescent desk lamp (~2 700 K).</summary>
    public static readonly Color WarmIncandescent = Convert(2700f);

    /// <summary>Cool-white office fluorescent (~4 000 K).</summary>
    public static readonly Color CoolFluorescent  = Convert(4000f);

    /// <summary>Neutral daylight white (~5 500 K).</summary>
    public static readonly Color DaylightWhite    = Convert(5500f);

    /// <summary>Server LED (cool blue-white, ~6 500 K).</summary>
    public static readonly Color ServerLedWhite   = Convert(6500f);

    /// <summary>Dawn / dusk warm orange (~2 000 K).</summary>
    public static readonly Color DawnOrange       = Convert(2000f);

    /// <summary>Noon sun white-blue (~6 000 K).</summary>
    public static readonly Color NoonSunWhite     = Convert(6000f);

    /// <summary>Night sky deep blue (~8 500 K).</summary>
    public static readonly Color NightSkyBlue     = Convert(8500f);
}
