// WP-4.0.A — URP Migration Foundation
// ======================================
// This editor script runs once when Unity opens the project on the sonnet-wp-4.0.a branch.
// It creates the URP pipeline assets and sets them as the active render pipeline in both
// Graphics Settings and all Quality tiers.
//
// LIFECYCLE
// ──────────
// On first open: URP assets don't exist → script creates them → sets GraphicsSettings +
//   QualitySettings → logs confirmation.
// On subsequent opens: assets already exist → script verifies settings → skips if already
//   correct.
//
// Talon: after visual verification passes, this file can be deleted (it will no longer do
// anything meaningful once the assets exist and settings are correct, but it's harmless to
// leave in place).

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class UrpSetupEditor
{
    private const string PipelineAssetPath = "Assets/Settings/URP-PipelineAsset.asset";
    private const string RendererDataPath  = "Assets/Settings/URP-PipelineAsset_Renderer.asset";

    static UrpSetupEditor()
    {
        EditorApplication.delayCall += EnsureUrpActive;
    }

    private static void EnsureUrpActive()
    {
        var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);

        if (pipelineAsset == null)
        {
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererDataPath);

            pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log("[WP-4.0.A] URP pipeline assets created at Assets/Settings/.");
        }

        bool changed = false;

        if (GraphicsSettings.renderPipelineAsset != pipelineAsset)
        {
            GraphicsSettings.renderPipelineAsset = pipelineAsset;
            changed = true;
        }

        for (int i = 0; i < QualitySettings.count; i++)
        {
            if (QualitySettings.GetRenderPipelineAssetAt(i) != pipelineAsset)
            {
                QualitySettings.SetRenderPipelineAssetAt(i, pipelineAsset);
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();
            Debug.Log("[WP-4.0.A] Graphics and Quality Settings updated to use URP. " +
                      "Run the Render Pipeline Converter (Window > Rendering > Render Pipeline " +
                      "Converter) to upgrade materials, then open MainScene + sandbox scenes for " +
                      "visual parity verification.");
        }
    }
}
