using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using APIFramework.Components;

/// <summary>
/// Reads docs/c2-content/world-definitions/room-visual-identity.json at boot and
/// resolves floor / wall / door materials for each <see cref="RoomCategory"/>.
///
/// DESIGN
/// ───────
/// Materials are stored under Assets/Resources/Materials/ so they can be loaded via
/// Resources.Load&lt;Material&gt;() at runtime.  The JSON catalog specifies the per-category
/// assignments and the Resources-relative path for each named material.
///
/// If the catalog is missing or a category has no entry the loader falls back to the
/// configured defaults (OfficeTile floor, StructuralWall wall, RegularDoor door).
///
/// MOUNTING
/// ─────────
/// Add to any persistent GameObject (e.g. the SceneBootstrapper root).
/// Assign to RoomRectangleRenderer._visualIdentityLoader in the Inspector.
/// </summary>
public sealed class RoomVisualIdentityLoader : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("Path to room-visual-identity.json relative to repo root. " +
             "Leave blank to use the default docs/c2-content/world-definitions/ location.")]
    private string _catalogPathOverride;

    // ── Runtime caches ─────────────────────────────────────────────────────────

    private bool _loaded;

    private readonly Dictionary<RoomCategory, Material> _floorMaterials = new();
    private readonly Dictionary<RoomCategory, Material> _wallMaterials  = new();
    private readonly Dictionary<RoomCategory, Material> _doorMaterials  = new();

    private Material _defaultFloor;
    private Material _defaultWall;
    private Material _defaultDoor;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake() => LoadCatalog();

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Floor material for this category, or the catalog default.</summary>
    public Material GetFloorMaterial(RoomCategory category)
    {
        if (!_loaded) LoadCatalog();
        return _floorMaterials.TryGetValue(category, out var m) ? m : _defaultFloor;
    }

    /// <summary>Wall material for this category, or the catalog default.</summary>
    public Material GetWallMaterial(RoomCategory category)
    {
        if (!_loaded) LoadCatalog();
        return _wallMaterials.TryGetValue(category, out var m) ? m : _defaultWall;
    }

    /// <summary>Door material for this category, or the catalog default.</summary>
    public Material GetDoorMaterial(RoomCategory category)
    {
        if (!_loaded) LoadCatalog();
        return _doorMaterials.TryGetValue(category, out var m) ? m : _defaultDoor;
    }

    /// <summary>True once catalog has been loaded (even if it resulted in all-defaults).</summary>
    public bool IsLoaded => _loaded;

    // ── Loading ────────────────────────────────────────────────────────────────

    private void LoadCatalog()
    {
        _loaded = true;  // mark early so recursive calls don't re-enter

        string path = ResolveCatalogPath();
        if (path == null || !File.Exists(path))
        {
            Debug.LogWarning("[RoomVisualIdentityLoader] Catalog not found; all rooms use defaults.");
            ResolveDefaults(null);
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            var root    = JsonUtility.FromJson<CatalogRoot>(json);
            if (root == null)
            {
                Debug.LogWarning("[RoomVisualIdentityLoader] Failed to parse catalog; using defaults.");
                ResolveDefaults(null);
                return;
            }

            // Build name → Resources path lookup.
            var pathByName = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.materialPaths != null)
                foreach (var mp in root.materialPaths)
                    if (!string.IsNullOrWhiteSpace(mp.name) && !string.IsNullOrWhiteSpace(mp.resourcesPath))
                        pathByName[mp.name] = mp.resourcesPath;

            // Material cache keyed by resourcesPath to avoid double-loading.
            var matCache = new Dictionary<string, Material>(StringComparer.Ordinal);

            Material Resolve(string name)
            {
                if (string.IsNullOrWhiteSpace(name)) return null;
                if (!pathByName.TryGetValue(name, out var rpath)) return null;
                if (matCache.TryGetValue(rpath, out var cached)) return cached;
                var mat = Resources.Load<Material>(rpath);
                if (mat == null)
                    Debug.LogWarning($"[RoomVisualIdentityLoader] Material not found at Resources/{rpath}");
                matCache[rpath] = mat;
                return mat;
            }

            // Resolve catalog-level defaults.
            ResolveDefaults(new DefaultMats
            {
                Floor = Resolve(root.defaultFloorMaterial),
                Wall  = Resolve(root.defaultWallMaterial),
                Door  = Resolve(root.defaultDoorMaterial),
            });

            // Per-category overrides.
            if (root.roomCategories != null)
            {
                foreach (var entry in root.roomCategories)
                {
                    if (!Enum.TryParse<RoomCategory>(entry.roomCategory, out var cat))
                    {
                        Debug.LogWarning($"[RoomVisualIdentityLoader] Unknown RoomCategory '{entry.roomCategory}' in catalog.");
                        continue;
                    }

                    var floor = Resolve(entry.defaultFloorMaterial);
                    var wall  = Resolve(entry.defaultWallMaterial);
                    var door  = Resolve(entry.defaultDoorMaterial);

                    if (floor != null) _floorMaterials[cat] = floor;
                    if (wall  != null) _wallMaterials[cat]  = wall;
                    if (door  != null) _doorMaterials[cat]  = door;
                }
            }

            Debug.Log($"[RoomVisualIdentityLoader] Loaded catalog: {_floorMaterials.Count} floor, " +
                      $"{_wallMaterials.Count} wall, {_doorMaterials.Count} door category mappings.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RoomVisualIdentityLoader] Exception loading catalog: {ex.Message}");
            ResolveDefaults(null);
        }
    }

    private void ResolveDefaults(DefaultMats? d)
    {
        // Try to resolve hardcoded fallback paths if the caller passed null.
        _defaultFloor = d?.Floor  ?? Resources.Load<Material>("Materials/Floor_OfficeTile");
        _defaultWall  = d?.Wall   ?? Resources.Load<Material>("Materials/Wall_Structural");
        _defaultDoor  = d?.Door   ?? Resources.Load<Material>("Materials/Door_Regular");
    }

    private string ResolveCatalogPath()
    {
        if (!string.IsNullOrWhiteSpace(_catalogPathOverride))
            return Path.GetFullPath(_catalogPathOverride);

        // Walk up from Application.dataPath (=.../ECSUnity/Assets) to repo root.
        string dataPath  = Application.dataPath;           // .../ECSUnity/Assets
        string unityRoot = Path.GetDirectoryName(dataPath); // .../ECSUnity
        string repoRoot  = Path.GetDirectoryName(unityRoot);// repo root

        string candidate = Path.Combine(repoRoot, "docs", "c2-content", "world-definitions",
                                        "room-visual-identity.json");
        if (File.Exists(candidate)) return candidate;

        // Fallback: one level deeper (in case of worktree layout).
        string candidate2 = Path.Combine(unityRoot, "docs", "c2-content", "world-definitions",
                                         "room-visual-identity.json");
        return File.Exists(candidate2) ? candidate2 : null;
    }

    // ── Serialisable data model ────────────────────────────────────────────────

    [Serializable]
    private sealed class CatalogRoot
    {
        public string                 schemaVersion;
        public string                 defaultFloorMaterial;
        public string                 defaultWallMaterial;
        public string                 defaultDoorMaterial;
        public RoomCategoryEntry[]    roomCategories;
        public MaterialPathEntry[]    materialPaths;
    }

    [Serializable]
    private sealed class RoomCategoryEntry
    {
        public string roomCategory;
        public string defaultFloorMaterial;
        public string defaultWallMaterial;
        public string defaultDoorMaterial;
        public string trimMaterial;
    }

    [Serializable]
    private sealed class MaterialPathEntry
    {
        public string name;
        public string resourcesPath;
    }

    private struct DefaultMats
    {
        public Material Floor;
        public Material Wall;
        public Material Door;
    }
}
