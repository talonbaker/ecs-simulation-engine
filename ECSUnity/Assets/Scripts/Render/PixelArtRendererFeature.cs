using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP ScriptableRendererFeature that renders the world at a low internal resolution
/// then point-samples it back to screen, producing the early-2000s pixel-art look.
///
/// Add to a URP renderer data asset's Feature list. Use SandboxToggle.P to toggle
/// at runtime in the sandbox; in production, toggle isActive programmatically.
///
/// MAC-009 first consumer. See docs/c2-infrastructure/MOD-API-CANDIDATES.md.
/// </summary>
public class PixelArtRendererFeature : ScriptableRendererFeature
{
    public enum PixelArtPreset
    {
        Crisp,    // 480×270
        Chunky,   // 320×180 (default)
        Custom,   // Inspector-set width/height
    }

    [Serializable]
    public class Settings
    {
        [Tooltip("Crisp = 480×270  |  Chunky = 320×180 (default)  |  Custom = use customResolution.")]
        public PixelArtPreset preset = PixelArtPreset.Chunky;

        [Tooltip("Internal render resolution. Only used when preset is Custom.")]
        public Vector2Int customResolution = new Vector2Int(320, 180);

        [Tooltip("1×N or N×N palette texture. Assign DefaultPixelArtPalette.png.")]
        public Texture2D paletteTexture;

        [Tooltip("Snap each pixel to the nearest entry in paletteTexture.")]
        public bool paletteQuantize = true;

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
