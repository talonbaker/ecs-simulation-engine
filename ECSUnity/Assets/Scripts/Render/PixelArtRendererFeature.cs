using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP ScriptableRendererFeature that applies Bayer 4×4 ordered dithering at full
/// resolution, then snaps each pixel to the nearest palette entry.
/// Produces a retro look with smooth shadow gradients — no resolution reduction.
///
/// Add to a URP renderer data asset's Feature list. Use SandboxToggle.P to toggle
/// at runtime in the sandbox; in production, toggle isActive programmatically.
/// </summary>
public class PixelArtRendererFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Settings
    {
        [Tooltip("1×N or N×N palette texture. Assign DefaultPixelArtPalette.png.")]
        public Texture2D paletteTexture;

        [Tooltip("Snap each pixel to the nearest entry in paletteTexture.")]
        public bool paletteQuantize = true;

        [Range(0f, 1f), Tooltip("Bayer dither spread applied before palette snap. 0 = hard snap, 1 = full spread.")]
        public float ditherStrength = 0.25f;

        [Tooltip("Where in URP's frame the pass executes. AfterRenderingPostProcessing = 600.")]
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
    }

    [Tooltip("Pixel-art render pass settings.")]
    public Settings settings = new Settings();

    PixelArtRenderPass _pass;

    public override void Create()
    {
        _pass = new PixelArtRenderPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!isActive) return;
        _pass.renderPassEvent = settings.injectionPoint;
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        base.Dispose(disposing);
    }
}
