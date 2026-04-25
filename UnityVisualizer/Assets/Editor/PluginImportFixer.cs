using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs automatically every time Unity imports (or reimports) a DLL in
/// Assets/Plugins/.  Forces validateReferences = 0 on APIFramework.dll so
/// Unity's import validation never blocks the assembly from loading.
///
/// WHY THIS IS NEEDED
/// ------------------
/// Unity regenerates a plugin's .meta file whenever the DLL changes on disk.
/// The regenerated file resets validateReferences to 1, which causes Unity to
/// reject APIFramework.dll because it references Newtonsoft.Json (provided by
/// the com.unity.nuget.newtonsoft-json package, not a file in Assets/Plugins/).
///
/// PluginImporter does not expose validateReferences as a public C# property in
/// Unity 6, so we set it via SerializedObject (direct access to the serialized
/// YAML fields that back the .meta file).
/// </summary>
public class PluginImportFixer : AssetPostprocessor
{
    private static readonly string[] ManagedPlugins =
    {
        "Assets/Plugins/APIFramework.dll",
    };

    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (var asset in importedAssets)
        {
            foreach (var target in ManagedPlugins)
            {
                if (asset != target) continue;

                var importer = AssetImporter.GetAtPath(asset) as PluginImporter;
                if (importer == null) continue;

                // Access the serialized backing field directly.
                // "validateReferences" is the internal YAML key written to the .meta file.
                var so   = new SerializedObject(importer);
                var prop = so.FindProperty("validateReferences");

                if (prop != null && prop.boolValue)
                {
                    prop.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    importer.SaveAndReimport();
                    Debug.Log("[PluginImportFixer] validateReferences forced to false on " + asset);
                }
            }
        }
    }
}
