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
using UnityEngine.Rendering;
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

        // Set Graphics Settings via SerializedObject so the change persists to
        // ProjectSettings/GraphicsSettings.asset without requiring a manual save.
        var graphicsSO = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var srpProp = graphicsSO.FindProperty("m_CustomRenderPipeline");
        if (srpProp.objectReferenceValue != pipelineAsset)
        {
            srpProp.objectReferenceValue = pipelineAsset;
            graphicsSO.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        // Set all QualitySettings tiers via SerializedObject (QualitySettings.SetRenderPipelineAssetAt
        // is not available in all Unity 6 Editor contexts; SerializedObject is always reliable).
        var qualitySO = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var tiersArray = qualitySO.FindProperty("m_QualitySettings");
        for (int i = 0; i < tiersArray.arraySize; i++)
        {
            var tierProp = tiersArray.GetArrayElementAtIndex(i)
                                     .FindPropertyRelative("customRenderPipeline");
            if (tierProp.objectReferenceValue != pipelineAsset)
            {
                tierProp.objectReferenceValue = pipelineAsset;
                changed = true;
            }
        }
        if (changed)
            qualitySO.ApplyModifiedPropertiesWithoutUndo();

        if (changed)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[WP-4.0.A] Graphics and Quality Settings updated to use URP. " +
                      "Run the Render Pipeline Converter (Window > Rendering > Render Pipeline " +
                      "Converter) to upgrade materials, then open MainScene + sandbox scenes for " +
                      "visual parity verification.");
        }
    }
}
