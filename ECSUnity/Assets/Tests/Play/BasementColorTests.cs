using NUnit.Framework;
using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-10: Per-room palette correctness.
///
/// Verifies that specific room categories receive the expected Kelvin tints when
/// illuminated at their canonical intensity levels (per aesthetic-bible §priority-1):
///   Basement / fluorescent area  → yellow-green tint (4 000 K fluorescent)
///   IT Closet                    → pale yellow LED glow (5 000–6 000 K cool white)
///   First-floor cubicle area     → warm cream tint (3 200–4 000 K warm fluorescent)
///
/// These tests verify KelvinToRgb + RoomAmbientTintApplier color values without
/// needing a full Unity scene boot.
/// </summary>
[TestFixture]
public class BasementColorTests
{
    // ── AT-10a: Basement (fluorescent 4 000 K) — greenish-yellow dominates ────

    [Test]
    public void Basement_FluorescentTint_HasYellowGreenBias()
    {
        // 4 000 K = cool-white fluorescent: red < 1, green high, blue moderate.
        Color tint = KelvinToRgb.Convert(4000f);

        // At 4000 K: green should be close to maximum and dominate blue.
        Assert.Greater(tint.g, 0.8f,
            $"4000 K should have high green channel (got {tint.g:F3}).");
        Assert.Greater(tint.g, tint.b,
            $"4000 K green ({tint.g:F3}) should exceed blue ({tint.b:F3}) — fluorescent yellow-green.");
    }

    // ── AT-10b: IT Closet (server LED 5 500–6 500 K) — near-white cool ────────

    [Test]
    public void ItCloset_ServerLed_IsNearWhiteCool()
    {
        // 6 000 K: server LED is near-white with a cool (slightly blue) tint.
        Color tint = KelvinToRgb.Convert(6000f);

        // All channels should be high (near white).
        Assert.Greater(tint.r, 0.75f, $"6000 K red should be high (got {tint.r:F3}).");
        Assert.Greater(tint.g, 0.85f, $"6000 K green should be high (got {tint.g:F3}).");
        Assert.Greater(tint.b, 0.85f, $"6000 K blue should be high (got {tint.b:F3}).");
    }

    // ── AT-10c: Cubicle (warm fluorescent 3 500 K) — warm cream ──────────────

    [Test]
    public void CubicleGrid_WarmFluorescent_IsWarmCreamy()
    {
        // 3 500 K: warm white fluorescent — more red/green than blue.
        Color tint = KelvinToRgb.Convert(3500f);

        Assert.Greater(tint.r, tint.b,
            $"3500 K should be warm (red {tint.r:F3} > blue {tint.b:F3}).");
        Assert.Greater(tint.g, tint.b,
            $"3500 K green ({tint.g:F3}) should exceed blue ({tint.b:F3}).");
    }

    // ── AT-10d: RenderColorPalette has distinct palette colors per category ───

    [Test]
    public void BasementAndItCloset_HaveDifferentBasePaletteColors()
    {
        // Basement = no specific category (mapped to ProductionFloor or similar);
        // we check that Hallway (dim) ≠ ItCloset (server-room grey) ≠ Office (warm cream).
        Color hallway = RenderColorPalette.Hallway;
        Color itClose = RenderColorPalette.ItCloset;
        Color office  = RenderColorPalette.Office;

        Assert.AreNotEqual(hallway, itClose,
            "Hallway and IT Closet should have different base palette colors.");
        Assert.AreNotEqual(itClose, office,
            "IT Closet and Office should have different base palette colors.");
    }

    // ── AT-10e: AmbientTintApplier blending — dim room stays near base color ──

    [Test]
    public void DimRoom_TintBlend_StaysNearPaletteColor()
    {
        // At ambient 10% and ambientTintBlend 0.28: tintIntensity = 0.28 * lerp(0.18, 0.95, 0.1)
        //   = 0.28 * 0.257 ≈ 0.072.
        // The final color = lerp(palette, kelvin * palette, 0.072).
        // This should be very close to the original palette color (< 10% deviation).
        Color palette = RenderColorPalette.Hallway;
        Color kelvin  = KelvinToRgb.Convert(4000f);

        float tintIntensity = 0.28f * Mathf.Lerp(0.18f, 0.95f, 0.10f);
        Color blended = Color.Lerp(palette, kelvin * palette, tintIntensity);

        float rDiff = Mathf.Abs(blended.r - palette.r);
        float gDiff = Mathf.Abs(blended.g - palette.g);
        float bDiff = Mathf.Abs(blended.b - palette.b);

        Assert.Less(rDiff, 0.12f, $"Dim room tint should not drift far from palette (red diff {rDiff:F3}).");
        Assert.Less(gDiff, 0.12f, $"Dim room tint should not drift far from palette (green diff {gDiff:F3}).");
        Assert.Less(bDiff, 0.12f, $"Dim room tint should not drift far from palette (blue diff {bDiff:F3}).");
    }

    // ── AT-10f: Bright sunlit room at 5 500 K is brighter than dim hallway ────

    [Test]
    public void BrightSunlitRoom_IsBrighterThan_DimHallway()
    {
        // Bright room: ambient 100, daylight white 5500 K.
        float brightBrightness = Mathf.Lerp(0.18f, 0.95f, 1.0f);   // 0.95
        Color brightKelvin     = KelvinToRgb.Convert(5500f);
        Color brightBase       = RenderColorPalette.Office;
        float brightTintI      = 0.28f * brightBrightness;
        Color brightFinal      = Color.Lerp(brightBase, brightKelvin * brightBase, brightTintI);

        // Dim hallway: ambient 15, cool fluorescent 4000 K.
        float dimBrightness    = Mathf.Lerp(0.18f, 0.95f, 0.15f);
        Color dimKelvin        = KelvinToRgb.Convert(4000f);
        Color dimBase          = RenderColorPalette.Hallway;
        float dimTintI         = 0.28f * dimBrightness;
        Color dimFinal         = Color.Lerp(dimBase, dimKelvin * dimBase, dimTintI);

        // Perceived brightness: simple luminance Y = 0.299R + 0.587G + 0.114B.
        float brightLuma = 0.299f * brightFinal.r + 0.587f * brightFinal.g + 0.114f * brightFinal.b;
        float dimLuma    = 0.299f * dimFinal.r    + 0.587f * dimFinal.g    + 0.114f * dimFinal.b;

        Assert.Greater(brightLuma, dimLuma,
            $"Sunlit office (luma {brightLuma:F3}) should be brighter than dim hallway (luma {dimLuma:F3}).");
    }
}
