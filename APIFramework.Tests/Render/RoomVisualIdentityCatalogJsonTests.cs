using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Xunit;

namespace APIFramework.Tests.Render;

/// <summary>
/// AT-06 / AT-13: room-visual-identity.json validates; all RoomCategory values present;
/// material paths are non-empty; schema version is present.
/// </summary>
public class RoomVisualIdentityCatalogJsonTests
{
    private static readonly string? CatalogPath = ResolveCatalogPath();

    private static string? ResolveCatalogPath()
    {
        // The test runner's working directory is the repo root or a project subdirectory.
        // Walk up until we find docs/c2-content/world-definitions/room-visual-identity.json.
        string dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 8; i++)
        {
            string candidate = Path.Combine(dir, "docs", "c2-content", "world-definitions",
                                            "room-visual-identity.json");
            if (File.Exists(candidate)) return candidate;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        return null;
    }

    [Fact]
    public void CatalogJson_FileExists()
    {
        Assert.True(File.Exists(CatalogPath),
            $"room-visual-identity.json not found. Searched from {Directory.GetCurrentDirectory()}");
    }

    [Fact]
    public void CatalogJson_ParsesWithoutException()
    {
        Assert.True(File.Exists(CatalogPath));
        string text = File.ReadAllText(CatalogPath!);
        var doc = JObject.Parse(text);
        Assert.NotNull(doc);
    }

    [Fact]
    public void CatalogJson_HasSchemaVersion()
    {
        Assert.True(File.Exists(CatalogPath));
        var doc = JObject.Parse(File.ReadAllText(CatalogPath!));
        string? ver = doc["schemaVersion"]?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(ver), "schemaVersion must be present and non-empty.");
    }

    [Fact]
    public void CatalogJson_HasRoomCategoriesArray()
    {
        Assert.True(File.Exists(CatalogPath));
        var doc = JObject.Parse(File.ReadAllText(CatalogPath!));
        var cats = doc["roomCategories"] as JArray;
        Assert.NotNull(cats);
        Assert.True(cats.Count > 0, "roomCategories must have at least one entry.");
    }

    [Fact]
    public void CatalogJson_AllCoreRoomCategoriesPresent()
    {
        Assert.True(File.Exists(CatalogPath));
        var doc  = JObject.Parse(File.ReadAllText(CatalogPath!));
        var cats = (JArray)doc["roomCategories"]!;

        var present = new HashSet<string>();
        foreach (var entry in cats)
            present.Add(entry["roomCategory"]?.ToString() ?? "");

        // These are the categories most likely to appear in production scenes.
        foreach (var required in new[] { "CubicleGrid", "Bathroom", "Breakroom", "Hallway", "Office" })
        {
            Assert.True(present.Contains(required),
                $"roomCategories is missing required category '{required}'.");
        }
    }

    [Fact]
    public void CatalogJson_AllCategoryEntriesHaveMaterials()
    {
        Assert.True(File.Exists(CatalogPath));
        var doc  = JObject.Parse(File.ReadAllText(CatalogPath!));
        var cats = (JArray)doc["roomCategories"]!;

        foreach (var entry in cats)
        {
            string cat = entry["roomCategory"]?.ToString() ?? "(unknown)";
            Assert.False(string.IsNullOrWhiteSpace(entry["defaultFloorMaterial"]?.ToString()),
                $"Category '{cat}' missing defaultFloorMaterial.");
            Assert.False(string.IsNullOrWhiteSpace(entry["defaultWallMaterial"]?.ToString()),
                $"Category '{cat}' missing defaultWallMaterial.");
            Assert.False(string.IsNullOrWhiteSpace(entry["defaultDoorMaterial"]?.ToString()),
                $"Category '{cat}' missing defaultDoorMaterial.");
        }
    }

    [Fact]
    public void CatalogJson_HasMaterialPathsArray()
    {
        Assert.True(File.Exists(CatalogPath));
        var doc   = JObject.Parse(File.ReadAllText(CatalogPath!));
        var paths = doc["materialPaths"] as JArray;
        Assert.NotNull(paths);
        Assert.True(paths.Count >= 10, "materialPaths must contain all 10 material entries.");
    }

    [Fact]
    public void CatalogJson_AllMaterialPathsHaveNonEmptyResourcesPath()
    {
        Assert.True(File.Exists(CatalogPath));
        var doc   = JObject.Parse(File.ReadAllText(CatalogPath!));
        var paths = (JArray)doc["materialPaths"]!;

        foreach (var entry in paths)
        {
            string name = entry["name"]?.ToString() ?? "(unknown)";
            Assert.False(string.IsNullOrWhiteSpace(entry["resourcesPath"]?.ToString()),
                $"Material '{name}' has empty resourcesPath.");
        }
    }

    [Fact]
    public void CatalogJson_MaterialNamesMatchMaterialPathsKeys()
    {
        Assert.True(File.Exists(CatalogPath));
        var doc      = JObject.Parse(File.ReadAllText(CatalogPath!));
        var cats     = (JArray)doc["roomCategories"]!;
        var pathsArr = (JArray)doc["materialPaths"]!;

        // Build a set of declared material names.
        var declaredNames = new HashSet<string>();
        foreach (var entry in pathsArr)
            declaredNames.Add(entry["name"]?.ToString() ?? "");

        // Every material referenced in roomCategories must exist in materialPaths.
        foreach (var entry in cats)
        {
            string cat = entry["roomCategory"]?.ToString() ?? "(unknown)";
            foreach (var field in new[] { "defaultFloorMaterial", "defaultWallMaterial", "defaultDoorMaterial" })
            {
                string? matName = entry[field]?.ToString();
                if (string.IsNullOrWhiteSpace(matName)) continue;
                Assert.True(declaredNames.Contains(matName),
                    $"Category '{cat}' references material '{matName}' " +
                    $"(field '{field}') which is not in materialPaths.");
            }
        }
    }

    [Fact]
    public void CatalogJson_AllFiveMaterialTypesPresent()
    {
        Assert.True(File.Exists(CatalogPath));
        var doc      = JObject.Parse(File.ReadAllText(CatalogPath!));
        var pathsArr = (JArray)doc["materialPaths"]!;

        var names = new HashSet<string>();
        foreach (var e in pathsArr) names.Add(e["name"]?.ToString() ?? "");

        // 5 floor types.
        foreach (var n in new[] { "Carpet", "Linoleum", "OfficeTile", "Concrete", "Hardwood" })
            Assert.True(names.Contains(n), $"materialPaths missing floor type '{n}'.");

        // 3 wall types.
        foreach (var n in new[] { "CubicleWall", "StructuralWall", "WindowWall" })
            Assert.True(names.Contains(n), $"materialPaths missing wall type '{n}'.");

        // 2 door types.
        foreach (var n in new[] { "RegularDoor", "RestroomDoor" })
            Assert.True(names.Contains(n), $"materialPaths missing door type '{n}'.");
    }
}
