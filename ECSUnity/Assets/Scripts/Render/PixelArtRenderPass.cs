using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ScriptableRenderPass owned by <see cref="PixelArtRendererFeature"/>.
/// Down-samples camera color to a low-res RTHandle (optionally palette-quantizing),
/// then point-samples it back to fill the camera target — creating the pixel-art look.
/// </summary>
public class PixelArtRenderPass : ScriptableRenderPass, IDisposable
{
    static readonly int s_PaletteTexId    = Shader.PropertyToID("_PaletteTex");
    static readonly int s_PaletteCountId  = Shader.PropertyToID("_PaletteCount");

    readonly PixelArtRendererFeature.Settings _settings;
    Material _material;
    RTHandle _lowResHandle;
    bool _disposed;

    public PixelArtRenderPass(PixelArtRendererFeature.Settings settings)
    {
        _settings = settings;
        _material = CoreUtils.CreateEngineMaterial("Custom/PixelArtQuantize");
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
    {
        Vector2Int res = ResolveResolution();
        RenderTextureDescriptor desc = data.cameraData.cameraTargetDescriptor;
        desc.width           = res.x;
        desc.height          = res.y;
        desc.depthBufferBits = 0;
        desc.msaaSamples     = 1;
        // FilterMode.Point on the low-res handle ensures point-sampled upscale.
        RenderingUtils.ReAllocateHandleIfNeeded(
            ref _lowResHandle, desc,
            FilterMode.Point, TextureWrapMode.Clamp,
            name: "_PixelArtLowRes");
    }

    public override void Execute(ScriptableRenderContext ctx, ref RenderingData data)
    {
        if (_material == null || _lowResHandle == null) return;

        var cmd          = CommandBufferPool.Get("Pixel Art Pass");
        var cameraTarget = data.cameraData.renderer.cameraColorTargetHandle;

        if (_settings.paletteTexture != null)
        {
            _material.SetTexture(s_PaletteTexId, _settings.paletteTexture);
            _material.SetFloat(s_PaletteCountId, _settings.paletteTexture.width);
        }

        // Pass 0: down-sample + palette quantize.  Pass 1: down-sample only.
        int pass = (_settings.paletteQuantize && _settings.paletteTexture != null) ? 0 : 1;

        Blitter.BlitCameraTexture(cmd, cameraTarget, _lowResHandle, _material, pass);
        // Upscale back to camera target; FilterMode.Point on _lowResHandle does the work.
        Blitter.BlitCameraTexture(cmd, _lowResHandle, cameraTarget);

        ctx.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void OnCameraCleanup(CommandBuffer cmd) { }

    Vector2Int ResolveResolution() =>
        _settings.preset switch
        {
            PixelArtRendererFeature.PixelArtPreset.Crisp  => new Vector2Int(480, 270),
            PixelArtRendererFeature.PixelArtPreset.Chunky => new Vector2Int(320, 180),
            _                                              => _settings.customResolution,
        };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lowResHandle?.Release();
        if (_material != null) CoreUtils.Destroy(_material);
    }
}
