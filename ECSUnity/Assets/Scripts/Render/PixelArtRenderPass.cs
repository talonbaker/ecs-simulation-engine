using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class PixelArtRenderPass : ScriptableRenderPass, IDisposable
{
    static readonly int s_PaletteTexId   = Shader.PropertyToID("_PaletteTex");
    static readonly int s_PaletteCountId = Shader.PropertyToID("_PaletteCount");

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
        public Material material;
        public int pass;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_material == null) return;

        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData   = frameData.Get<UniversalCameraData>();

        Vector2Int res = ResolveResolution();

        if (_settings.paletteTexture != null)
        {
            _material.SetTexture(s_PaletteTexId, _settings.paletteTexture);
            _material.SetFloat(s_PaletteCountId, _settings.paletteTexture.width);
        }
        int downsamplePass = (_settings.paletteQuantize && _settings.paletteTexture != null) ? 0 : 1;

        var desc = cameraData.cameraTargetDescriptor;
        desc.width           = res.x;
        desc.height          = res.y;
        desc.depthBufferBits = 0;
        desc.msaaSamples     = 1;
        TextureHandle lowRes = UniversalRenderer.CreateRenderGraphTexture(
            renderGraph, desc, "_PixelArtLowRes", false, FilterMode.Point);

        // Pass A: downsample (+ optional palette quantize) to low-res buffer.
        using (var builder = renderGraph.AddRasterRenderPass<PassData>("PixelArt Downsample", out var passData))
        {
            passData.src      = resourceData.activeColorTexture;
            passData.material = _material;
            passData.pass     = downsamplePass;

            builder.UseTexture(passData.src);
            builder.SetRenderAttachment(lowRes, 0);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                Blitter.BlitTexture(ctx.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, data.pass));
        }

        // Pass B: point-filter upscale back to camera target (shader pass 2 uses sampler_PointClamp).
        using (var builder = renderGraph.AddRasterRenderPass<PassData>("PixelArt Upscale", out var passData))
        {
            passData.src      = lowRes;
            passData.material = _material;
            passData.pass     = 2;

            builder.UseTexture(passData.src);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                Blitter.BlitTexture(ctx.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, data.pass));
        }
    }

    Vector2Int ResolveResolution() =>
        _settings.preset switch
        {
            PixelArtRendererFeature.PixelArtPreset.Crisp   => new Vector2Int(480, 270),
            PixelArtRendererFeature.PixelArtPreset.Chunky  => new Vector2Int(320, 180),
            _                                               => _settings.customResolution,
        };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_material != null) CoreUtils.Destroy(_material);
    }
}
