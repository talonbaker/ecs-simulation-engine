using APIFramework.Systems.Animation;
using UnityEngine;

/// <summary>
/// Unity ScriptableObject wrapper that loads <see cref="NpcVisualStateCatalog"/>
/// from a TextAsset (the visual-state-catalog.json) at runtime.
///
/// USAGE
/// ──────
/// 1. Create via Assets → Create → ECS → NpcVisualStateCatalogLoader.
/// 2. Assign the TextAsset pointing to docs/c2-content/animation/visual-state-catalog.json.
/// 3. Reference this asset from <see cref="SilhouetteAnimator"/> and
///    <see cref="ChibiEmotionPopulator"/> via their Inspector fields.
///
/// FALLBACK
/// ─────────
/// If no TextAsset is assigned, the loader attempts to find the JSON by walking up
/// from the application's data path. If still not found, an empty catalog is
/// returned — callers receive safe defaults rather than null or exceptions.
/// </summary>
[CreateAssetMenu(
    menuName = "ECS/NpcVisualStateCatalogLoader",
    fileName = "NpcVisualStateCatalogLoader")]
public sealed class NpcVisualStateCatalogLoader : ScriptableObject
{
    [SerializeField]
    [Tooltip("TextAsset pointing to docs/c2-content/animation/visual-state-catalog.json.")]
    private TextAsset _jsonAsset;

    private NpcVisualStateCatalog _catalog;

    /// <summary>
    /// The loaded catalog. Populated on first access.
    /// Falls back to empty catalog if JSON is absent or unparseable.
    /// </summary>
    public NpcVisualStateCatalog Catalog
    {
        get
        {
            if (_catalog != null) return _catalog;

            if (_jsonAsset != null)
            {
                _catalog = NpcVisualStateCatalogLoader_Pure.ParseJson(_jsonAsset.text);
            }
            else
            {
                var path = NpcVisualStateCatalogLoader_Pure.FindDefaultPath();
                _catalog = path != null
                    ? NpcVisualStateCatalogLoader_Pure.Load(path)
                    : NpcVisualStateCatalogLoader_Pure.Empty;

                if (path == null)
                    Debug.LogWarning("[NpcVisualStateCatalogLoader] visual-state-catalog.json not found via path search; " +
                                     "using empty catalog defaults. Assign the TextAsset in the Inspector.");
            }

            return _catalog;
        }
    }

    /// <summary>Empty catalog with safe defaults; used as fallback when JSON is unavailable.</summary>
    public static NpcVisualStateCatalog Empty => NpcVisualStateCatalogLoader_Pure.Empty;

    /// <summary>Parses a catalog from a JSON string.</summary>
    public static NpcVisualStateCatalog ParseJson(string json) => NpcVisualStateCatalogLoader_Pure.ParseJson(json);

    /// <summary>Loads a catalog from a file path.</summary>
    public static NpcVisualStateCatalog Load(string path) => NpcVisualStateCatalogLoader_Pure.Load(path);

    /// <summary>Finds the default catalog JSON path by walking up from the application data path.</summary>
    public static string FindDefaultPath() => NpcVisualStateCatalogLoader_Pure.FindDefaultPath();

    // Expose pure-C# loader under a distinct name to avoid collision with the class name.
    private static class NpcVisualStateCatalogLoader_Pure
    {
        public static string               FindDefaultPath() => APIFramework.Systems.Animation.NpcVisualStateCatalogLoader.FindDefaultPath();
        public static NpcVisualStateCatalog Load(string path) => APIFramework.Systems.Animation.NpcVisualStateCatalogLoader.Load(path);
        public static NpcVisualStateCatalog ParseJson(string json) => APIFramework.Systems.Animation.NpcVisualStateCatalogLoader.ParseJson(json);
        public static NpcVisualStateCatalog Empty => APIFramework.Systems.Animation.NpcVisualStateCatalogLoader.Empty;
    }

    private void OnValidate()
    {
        // Force reload when the Inspector changes the TextAsset.
        _catalog = null;
    }
}
