using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

        public OutlineStubRenderPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing + 1;
            _mat = CoreUtils.CreateEngineMaterial("Custom/OutlineStub");
        }

        public override void Execute(ScriptableRenderContext ctx, ref RenderingData data)
        {
            if (_mat == null) return;
            var cmd    = CommandBufferPool.Get("Outline Stub Pass");
            var target = data.cameraData.renderer.cameraColorTargetHandle;
            Blitter.BlitCameraTexture(cmd, target, target, _mat, 0);
            ctx.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd) { }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_mat != null) CoreUtils.Destroy(_mat);
        }
    }
}
