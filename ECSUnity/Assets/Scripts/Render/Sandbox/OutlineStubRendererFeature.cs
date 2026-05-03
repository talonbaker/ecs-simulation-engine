using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// STUB — sandbox chain-validation only. Not integrated into MainScene or PlaytestScene.
/// Applies a visible red vignette at screen edges to prove the second feature in the
/// SandboxURP-Renderer chain executes after PixelArtRendererFeature.
/// </summary>
public class OutlineStubRendererFeature : ScriptableRendererFeature
{
    OutlineStubRenderPass _pass;

    public override void Create()
    {
        _pass = new OutlineStubRenderPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!isActive) return;
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        base.Dispose(disposing);
    }

    // ── Inner pass ──────────────────────────────────────────────────────────

    sealed class OutlineStubRenderPass : ScriptableRenderPass, IDisposable
    {
        Material _mat;
        bool _disposed;

        class PassData { public TextureHandle src; public Material mat; }

        public OutlineStubRenderPass()
        {
            renderPassEvent          = RenderPassEvent.AfterRenderingPostProcessing + 1;
            _mat                     = CoreUtils.CreateEngineMaterial("Custom/OutlineStub");
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_mat == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData   = frameData.Get<UniversalCameraData>();

            // Temporary copy so we can read and write camera color in separate passes.
            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples     = 1;
            TextureHandle tempHandle = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_OutlineStubTemp", false);

            // Step 1: copy active color to temp (shader pass 1 = plain blit)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("OutlineStub Copy", out var passData))
            {
                passData.src = resourceData.activeColorTexture;
                passData.mat = mat;
                builder.UseTexture(passData.src);
                builder.SetRenderAttachment(tempHandle, 0);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, data.src, new Vector4(1, 1, 0, 0), data.mat, 1));
            }

            // Step 2: apply red vignette from temp back to active color
            Material mat = _mat;
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("OutlineStub Vignette", out var passData))
            {
                passData.src = tempHandle;
                passData.mat = mat;
                builder.UseTexture(passData.src);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, data.src, new Vector4(1, 1, 0, 0), data.mat, 0));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_mat != null) CoreUtils.Destroy(_mat);
        }
    }
}
