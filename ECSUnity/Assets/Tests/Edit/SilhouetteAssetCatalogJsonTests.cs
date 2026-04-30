using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AT-09: silhouette-catalog.json loads; all cast-bible archetypes are present;
/// all dominantColor values are valid CSS hex strings.
///
/// EDIT-MODE test — reads the file from disk; no play loop.
///
/// Expected archetype IDs (from cast-bible.md — 10 archetypes):
///   the-old-hand, the-hermit, the-cynic, the-vent, the-climber,
///   the-newbie, the-affair, the-recovering, the-founders-nephew, the-crush
///
/// The JSON file lives at:
///   docs/c2-content/silhouette-catalog.json
/// Resolved relative to Application.dataPath (Assets/) then walking up to project root.
/// </summary>
[TestFixture]
public class SilhouetteAssetCatalogJsonTests
{
    // All archetype IDs from cast-bible.md that must appear in the catalog.
    private static readonly string[] ExpectedArchetypeIds =
    {
        "the-old-hand",
        "the-hermit",
        "the-cynic",
        "the-vent",
        "the-climber",
        "the-newbie",
        "the-affair",
        "the-recovering",
        "the-founders-nephew",
        "the-crush",
    };

    // Regex for valid CSS hex colors: #RRGGBB or #RGB (case-insensitive).
    private static readonly Regex HexColorPattern =
        new Regex(@"^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})$", RegexOptions.Compiled);

    private string _catalogJson;
    private CatalogRoot _catalog;

    [SetUp]
    public void SetUp()
    {
        string catalogPath = ResolveCatalogPath();
        Assert.IsNotNull(catalogPath,
            "silhouette-catalog.json not found. Expected at docs/c2-content/silhouette-catalog.json " +
            "relative to the Unity project root.");

        _catalogJson = File.ReadAllText(catalogPath);
        Assert.IsNotEmpty(_catalogJson, "silhouette-catalog.json must not be empty.");

        // Use Unity's JsonUtility via a wrapper, or Newtonsoft.Json if available.
        // Newtonsoft is listed as a precompiled reference in the test asmdef.
        _catalog = Newtonsoft.Json.JsonConvert.DeserializeObject<CatalogRoot>(_catalogJson);
        Assert.IsNotNull(_catalog, "Failed to deserialize silhouette-catalog.json.");
        Assert.IsNotNull(_catalog.archetypes, "archetypes array must not be null.");
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Test]
    public void Catalog_Deserializes_Successfully()
    {
        Assert.IsNotNull(_catalog);
        Assert.IsNotNull(_catalog.archetypes);
        Debug.Log($"[SilhouetteAssetCatalogJsonTests] Loaded {_catalog.archetypes.Length} archetypes.");
    }

    [Test]
    public void Catalog_SchemaVersion_IsPresent()
    {
        Assert.IsNotEmpty(_catalog.schemaVersion,
            "silhouette-catalog.json must have a non-empty schemaVersion field.");
    }

    [Test]
    public void Catalog_ContainsAllCastBibleArchetypes()
    {
        var presentIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _catalog.archetypes)
            if (!string.IsNullOrWhiteSpace(entry.archetypeId))
                presentIds.Add(entry.archetypeId);

        foreach (var expected in ExpectedArchetypeIds)
        {
            Assert.IsTrue(presentIds.Contains(expected),
                $"silhouette-catalog.json is missing archetype '{expected}'. " +
                $"All 10 cast-bible archetypes must be present (AT-09).");
        }
    }

    [Test]
    public void Catalog_AllDominantColors_AreValidHex()
    {
        foreach (var entry in _catalog.archetypes)
        {
            Assert.IsNotEmpty(entry.dominantColor,
                $"Archetype '{entry.archetypeId}' has empty dominantColor.");
            Assert.IsTrue(HexColorPattern.IsMatch(entry.dominantColor),
                $"Archetype '{entry.archetypeId}' dominantColor '{entry.dominantColor}' " +
                $"is not a valid CSS hex color (#RRGGBB or #RGB).");
        }
    }

    [Test]
    public void Catalog_AllArchetypeIds_AreNonEmpty()
    {
        foreach (var entry in _catalog.archetypes)
        {
            Assert.IsNotEmpty(entry.archetypeId,
                "Every archetype entry must have a non-empty archetypeId.");
        }
    }

    [Test]
    public void Catalog_AllArchetypeIds_AreUnique()
    {
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _catalog.archetypes)
        {
            Assert.IsFalse(seen.Contains(entry.archetypeId),
                $"Duplicate archetypeId '{entry.archetypeId}' found in silhouette-catalog.json.");
            seen.Add(entry.archetypeId);
        }
    }

    [Test]
    public void Catalog_AllHeightValues_AreRecognised()
    {
        var valid = new HashSet<string> { "short", "average", "tall" };
        foreach (var entry in _catalog.archetypes)
        {
            Assert.IsTrue(
                string.IsNullOrWhiteSpace(entry.height) || valid.Contains(entry.height),
                $"Archetype '{entry.archetypeId}' height '{entry.height}' " +
                $"is not one of: short / average / tall.");
        }
    }

    [Test]
    public void Catalog_AllBuildValues_AreRecognised()
    {
        var valid = new HashSet<string> { "slight", "average", "stocky" };
        foreach (var entry in _catalog.archetypes)
        {
            Assert.IsTrue(
                string.IsNullOrWhiteSpace(entry.build) || valid.Contains(entry.build),
                $"Archetype '{entry.archetypeId}' build '{entry.build}' " +
                $"is not one of: slight / average / stocky.");
        }
    }

    [Test]
    public void ParseHexColor_KnownAnchors_RoundTrip()
    {
        // Validate that the known anchor colors from WP AT-03 parse correctly.
        Assert.IsTrue(HexColorPattern.IsMatch("#5C2A4B"), "Donna color invalid");
        Assert.IsTrue(HexColorPattern.IsMatch("#A8C070"), "Greg color invalid");
        Assert.IsTrue(HexColorPattern.IsMatch("#7B5A3A"), "Frank color invalid");
    }

    // ── Path resolution ────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the path to silhouette-catalog.json.
    /// In the Unity editor, Application.dataPath is the Assets/ folder.
    /// We walk up to the project root and find docs/c2-content/silhouette-catalog.json.
    /// </summary>
    private static string ResolveCatalogPath()
    {
        // Application.dataPath = .../ECSUnity/Assets
        // Project root = .../ECSUnity/..  or the git repo root two levels up
        string dataPath   = Application.dataPath;   // .../ECSUnity/Assets
        string unityRoot  = Path.GetDirectoryName(dataPath);            // .../ECSUnity
        string repoRoot   = Path.GetDirectoryName(unityRoot);           // repo root

        string candidate1 = Path.Combine(repoRoot, "docs", "c2-content", "silhouette-catalog.json");
        if (File.Exists(candidate1)) return candidate1;

        string candidate2 = Path.Combine(unityRoot, "docs", "c2-content", "silhouette-catalog.json");
        if (File.Exists(candidate2)) return candidate2;

        return null;
    }

    // ── Inner deserialization types ────────────────────────────────────────────

    [System.Serializable]
    private sealed class CatalogRoot
    {
        public string           schemaVersion;
        public ArchetypeEntry[] archetypes;
    }

    [System.Serializable]
    private sealed class ArchetypeEntry
    {
        public string archetypeId;
        public string height;
        public string build;
        public string hairLength;
        public string hairColor;
        public string hair;
        public string headwear;
        public string item;
        public string dominantColor;
    }
}
