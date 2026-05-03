using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// AT-01 / AT-02 / AT-03 — URP pipeline configuration verification.
///
/// AT-01: manifest.json lists com.unity.render-pipelines.universal at 17.x.
/// AT-02: GraphicsSettings uses a UniversalRenderPipelineAsset.
/// AT-03: All quality tiers in QualitySettings use the URP pipeline asset.
///
/// These tests pass after UrpSetupEditor.cs runs on first open (which creates the URP
/// pipeline assets, sets GraphicsSettings, and sets all quality tiers). They are
/// expected to fail on a cold clone that has never been opened in Unity Editor.
///
/// NOTE ON AT-01
/// ─────────────
/// Reads the manifest file directly so the test is meaningful even before Unity
/// imports the package (e.g., in a headless CI environment where the package
/// registry has not been consulted yet).
/// </summary>
[TestFixture]
public class UrpPipelineConfigTests
{
    // ── AT-01 ─────────────────────────────────────────────────────────────────

    [Test]
    public void Manifest_ContainsUrp17Package()
    {
        string manifestPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../../Packages/manifest.json"));

        Assert.IsTrue(File.Exists(manifestPath),
            $"manifest.json not found at expected path: {manifestPath}");

        string content = File.ReadAllText(manifestPath);

        Assert.IsTrue(content.Contains("com.unity.render-pipelines.universal"),
            "manifest.json must contain 'com.unity.render-pipelines.universal'. " +
            "WP-4.0.A adds this package to migrate ECSUnity from built-in to URP.");

        // Verify it's version 17.x (Unity 6 LTS-aligned URP).
        Assert.IsTrue(content.Contains("\"17."),
            "com.unity.render-pipelines.universal must be version 17.x " +
            "(Unity 6 LTS-aligned URP).");
    }

    // ── AT-02 ─────────────────────────────────────────────────────────────────

    [Test]
    public void GraphicsSettings_UsesUrpPipelineAsset()
    {
        var pipeline = GraphicsSettings.defaultRenderPipeline;

        Assert.IsNotNull(pipeline,
            "GraphicsSettings.renderPipelineAsset must not be null. " +
            "UrpSetupEditor.cs sets this when Unity first opens the project on this branch.");

        Assert.IsInstanceOf<UniversalRenderPipelineAsset>(pipeline,
            $"Active render pipeline must be UniversalRenderPipelineAsset. " +
            $"Got: {pipeline?.GetType().FullName ?? "null"}");
    }

    // ── AT-03 ─────────────────────────────────────────────────────────────────

    [Test]
    public void QualitySettings_AllTiersUseUrpPipelineAsset()
    {
        string[] tierNames = QualitySettings.names;

        Assert.Greater(tierNames.Length, 0,
            "QualitySettings must have at least one quality tier.");

        for (int i = 0; i < tierNames.Length; i++)
        {
            var tierPipeline = QualitySettings.GetRenderPipelineAssetAt(i);

            Assert.IsNotNull(tierPipeline,
                $"Quality tier '{tierNames[i]}' (index {i}) has no render pipeline asset. " +
                "UrpSetupEditor.cs sets all tiers to the URP asset on first open.");

            Assert.IsInstanceOf<UniversalRenderPipelineAsset>(tierPipeline,
                $"Quality tier '{tierNames[i]}' (index {i}) must use UniversalRenderPipelineAsset. " +
                $"Got: {tierPipeline?.GetType().FullName ?? "null"}");
        }
    }

    // ── Additional structural checks ──────────────────────────────────────────

    [Test]
    public void UrpPipelineAsset_HasRendererData()
    {
        var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;

        if (pipeline == null)
        {
            Assert.Inconclusive("URP pipeline asset not set — run AT-02 first.");
            return;
        }

        // The renderer asset is the Universal Renderer Data attached to the pipeline.
        // A correctly created URP asset always has at least one renderer data entry.
        Assert.IsNotNull(pipeline.scriptableRenderer,
            "URP pipeline asset must have a scriptable renderer (UniversalRendererData). " +
            "UrpSetupEditor.cs creates URP-PipelineAsset_Renderer.asset and wires it in.");
    }
}
