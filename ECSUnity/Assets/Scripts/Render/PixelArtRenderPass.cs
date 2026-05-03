using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class PixelArtRenderPass : ScriptableRenderPass, IDisposable
{
    static readonly int s_PaletteTexId     = Shader.PropertyToID("_PaletteTex");
    static readonly int s_PaletteCountId   = Shader.PropertyToID("_PaletteCount");
    static readonly int s_DitherStrengthId = Shader.PropertyToID("_DitherStrength");

    readonly PixelArtRendererFeature.Settings _settings;
    Material _material;
    bool _disposed;

    public PixelArtRenderPass(PixelArtRendererFeature.Settings settings)
    {
        _settings = settings;
        _material = CoreUtils.CreateEngineMaterial("Custom/PixelArtQuantize");
        requiresIntermediateTexture = true;
    }

    class PassData
    {
        public TextureHandle src;
        public Material      material;
        public int           pass;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_material == null) return;

        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData   = frameData.Get<UniversalCameraData>();

        if (_settings.paletteTexture != null)
        {
            _material.SetTexture(s_PaletteTexId,   _settings.paletteTexture);
            _material.SetFloat(s_PaletteCountId,   _settings.paletteTexture.width);
        }
        _material.SetFloat(s_DitherStrengthId, _settings.ditherStrength);

        // Pass 0 = dither + palette snap; pass 1 = plain blit (fallback when no palette).
        int ditherPass = (_settings.paletteQuantize && _settings.paletteTexture != null) ? 0 : 1;

        var desc = cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;
        desc.msaaSamples     = 1;
        TextureHandle tempHandle = UniversalRenderer.CreateRenderGraphTexture(
            renderGraph, desc, "_PixelArtTemp", false);

        // Step 1: copy active color to temp (pass 1 = plain blit).
        using (var builder = renderGraph.AddRasterRenderPass<PassData>("PixelArt Copy", out var passData))
        {
            passData.src      = resourceData.activeColorTexture;
            passData.material = _material;
            passData.pass     = 1;

            builder.UseTexture(passData.src);
            builder.SetRenderAttachment(tempHandle, 0);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                Blitter.BlitTexture(ctx.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, data.pass));
        }

        // Step 2: Bayer-dither from temp back to active color.
        using (var builder = renderGraph.AddRasterRenderPass<PassData>("PixelArt Dither", out var passData))
        {
            passData.src      = tempHandle;
            passData.material = _material;
            passData.pass     = ditherPass;

            builder.UseTexture(passData.src);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                Blitter.BlitTexture(ctx.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, data.pass));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_material != null) CoreUtils.Destroy(_material);
    }
}
