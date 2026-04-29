using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AT-13: docs/c2-content/build-palette-catalog.json loads; all categories present;
/// named-anchor uniqueness flags valid.
/// </summary>
[TestFixture]
public class BuildPaletteCatalogJsonTests
{
    private string _jsonPath;

    [SetUp]
    public void SetUp()
    {
        // Path relative to repository root (works both in editor and CI).
        _jsonPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "..", "..",
                "docs", "c2-content", "build-palette-catalog.json"));
    }

    [Test]
    public void CatalogJsonFile_Exists()
    {
        Assert.IsTrue(File.Exists(_jsonPath),
            $"build-palette-catalog.json not found at: {_jsonPath}");
    }

    [Test]
    public void CatalogJson_ParsesWithoutException()
    {
        string text = File.ReadAllText(_jsonPath);
        Assert.IsNotNull(text);
        Assert.IsNotEmpty(text);

        // Simple structural check — parse the JSON with Newtonsoft.Json.
        var doc = Newtonsoft.Json.Linq.JObject.Parse(text);
        Assert.IsNotNull(doc, "JSON should parse into a JObject without errors.");
    }

    [Test]
    public void CatalogJson_HasEntriesArray()
    {
        string text = File.ReadAllText(_jsonPath);
        var doc     = Newtonsoft.Json.Linq.JObject.Parse(text);
        var entries = doc["entries"] as Newtonsoft.Json.Linq.JArray;
        Assert.IsNotNull(entries, "JSON must have a top-level 'entries' array.");
        Assert.Greater(entries.Count, 0, "entries array must have at least one item.");
    }

    [Test]
    public void CatalogJson_AllFourCategoriesPresent()
    {
        string text = File.ReadAllText(_jsonPath);
        var doc     = Newtonsoft.Json.Linq.JObject.Parse(text);
        var entries = (Newtonsoft.Json.Linq.JArray)doc["entries"]!;

        var categories = new HashSet<string>();
        foreach (var entry in entries)
            categories.Add(entry["category"]?.ToString() ?? string.Empty);

        Assert.IsTrue(categories.Contains("Structural"),  "Category 'Structural' missing.");
        Assert.IsTrue(categories.Contains("Furniture"),   "Category 'Furniture' missing.");
        Assert.IsTrue(categories.Contains("Props"),       "Category 'Props' missing.");
        Assert.IsTrue(categories.Contains("NamedAnchor"), "Category 'NamedAnchor' missing.");
    }

    [Test]
    public void CatalogJson_AllEntriesHaveLabel()
    {
        string text = File.ReadAllText(_jsonPath);
        var doc     = Newtonsoft.Json.Linq.JObject.Parse(text);
        var entries = (Newtonsoft.Json.Linq.JArray)doc["entries"]!;

        int i = 0;
        foreach (var entry in entries)
        {
            string label = entry["label"]?.ToString() ?? string.Empty;
            Assert.IsFalse(string.IsNullOrWhiteSpace(label),
                $"Entry #{i} is missing a label.");
            i++;
        }
    }

    [Test]
    public void CatalogJson_UniqueAnchorsMustHaveFlagSet()
    {
        string text = File.ReadAllText(_jsonPath);
        var doc     = Newtonsoft.Json.Linq.JObject.Parse(text);
        var entries = (Newtonsoft.Json.Linq.JArray)doc["entries"]!;

        // Every NamedAnchor entry must have uniqueInstance = true.
        foreach (var entry in entries)
        {
            if (entry["category"]?.ToString() == "NamedAnchor")
            {
                bool unique = entry["uniqueInstance"]?.Value<bool>() ?? false;
                string label = entry["label"]?.ToString() ?? "(unknown)";
                Assert.IsTrue(unique,
                    $"NamedAnchor entry '{label}' must have uniqueInstance = true.");
            }
        }
    }

    [Test]
    public void CatalogJson_AllTemplateIdsAreValidGuids()
    {
        string text = File.ReadAllText(_jsonPath);
        var doc     = Newtonsoft.Json.Linq.JObject.Parse(text);
        var entries = (Newtonsoft.Json.Linq.JArray)doc["entries"]!;

        foreach (var entry in entries)
        {
            string idStr = entry["templateId"]?.ToString() ?? string.Empty;
            string label = entry["label"]?.ToString() ?? "(unknown)";
            Assert.IsTrue(System.Guid.TryParse(idStr, out _),
                $"Entry '{label}' has invalid templateId: '{idStr}'.");
        }
    }
}
