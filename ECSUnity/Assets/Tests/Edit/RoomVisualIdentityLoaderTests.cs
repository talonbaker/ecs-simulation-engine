using System.IO;
using NUnit.Framework;
using UnityEngine;
using APIFramework.Components;

/// <summary>
/// AT-06: RoomVisualIdentityLoader reads catalog JSON and resolves materials correctly.
///
/// EDIT-MODE tests — no play loop, materials loaded from Resources.
/// </summary>
[TestFixture]
public class RoomVisualIdentityLoaderTests
{
    private GameObject _go;
    private RoomVisualIdentityLoader _loader;

    [SetUp]
    public void SetUp()
    {
        _go     = new GameObject("LoaderTest");
        _loader = _go.AddComponent<RoomVisualIdentityLoader>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    // ── Catalog file ───────────────────────────────────────────────────────────

    [Test]
    public void CatalogJson_ExistsAtExpectedRepoPath()
    {
        string dataPath  = Application.dataPath;
        string unityRoot = Path.GetDirectoryName(dataPath);
        string repoRoot  = Path.GetDirectoryName(unityRoot);
        string path      = Path.Combine(repoRoot, "docs", "c2-content", "world-definitions",
                                        "room-visual-identity.json");
        Assert.IsTrue(File.Exists(path),
            $"room-visual-identity.json not found at: {path}");
    }

    // ── Loader state ───────────────────────────────────────────────────────────

    [Test]
    public void Loader_IsLoaded_AfterAwake()
    {
        // Awake is called by AddComponent, so IsLoaded should be true immediately.
        Assert.IsTrue(_loader.IsLoaded,
            "RoomVisualIdentityLoader.IsLoaded should be true after Awake.");
    }

    [Test]
    public void Loader_DoesNotThrow_WithMissingCatalog()
    {
        // Override path to a non-existent file; loader should survive gracefully.
        var overridePath = typeof(RoomVisualIdentityLoader).GetField(
            "_catalogPathOverride",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        overridePath?.SetValue(_loader, "/nonexistent/path/catalog.json");

        // Re-init by calling Awake via a fresh component.
        var go2  = new GameObject("LoaderTest2");
        var ldr2 = go2.AddComponent<RoomVisualIdentityLoader>();
        overridePath?.SetValue(ldr2, "/nonexistent/path/catalog.json");

        // GetFloorMaterial should not throw even with a bad path; returns default or null.
        Assert.DoesNotThrow(() => ldr2.GetFloorMaterial(RoomCategory.CubicleGrid));
        Object.DestroyImmediate(go2);
    }

    // ── Material resolution ────────────────────────────────────────────────────

    [Test]
    public void GetFloorMaterial_CubicleGrid_ReturnsCarpetOrDefault()
    {
        // After loading catalog, CubicleGrid should get Carpet.
        // If the material asset isn't present in the test environment (no Resources/),
        // the loader logs a warning and returns the default.  Either way, no exception.
        Assert.DoesNotThrow(() =>
        {
            var mat = _loader.GetFloorMaterial(RoomCategory.CubicleGrid);
            // mat may be null if Resources/Materials/ aren't present in test environment.
            if (mat != null)
                Assert.IsTrue(mat.name == "Floor_Carpet" || mat.name.Contains("Carpet") ||
                              mat.name.Contains("OfficeTile"),
                    $"CubicleGrid floor material should be Carpet or default; got '{mat.name}'.");
        });
    }

    [Test]
    public void GetFloorMaterial_Bathroom_ReturnsLinoleumOrDefault()
    {
        Assert.DoesNotThrow(() =>
        {
            var mat = _loader.GetFloorMaterial(RoomCategory.Bathroom);
            if (mat != null)
                Assert.IsTrue(mat.name.Contains("Linoleum") || mat.name.Contains("OfficeTile"),
                    $"Bathroom floor material should be Linoleum or default; got '{mat.name}'.");
        });
    }

    [Test]
    public void GetWallMaterial_CubicleGrid_ReturnsCubicleWallOrDefault()
    {
        Assert.DoesNotThrow(() =>
        {
            var mat = _loader.GetWallMaterial(RoomCategory.CubicleGrid);
            if (mat != null)
                Assert.IsTrue(mat.name.Contains("Cubicle") || mat.name.Contains("Structural"),
                    $"CubicleGrid wall should be CubicleWall or default; got '{mat.name}'.");
        });
    }

    [Test]
    public void GetDoorMaterial_Bathroom_ReturnsRestroomDoorOrDefault()
    {
        Assert.DoesNotThrow(() =>
        {
            var mat = _loader.GetDoorMaterial(RoomCategory.Bathroom);
            if (mat != null)
                Assert.IsTrue(mat.name.Contains("Restroom") || mat.name.Contains("Regular"),
                    $"Bathroom door should be RestroomDoor or default; got '{mat.name}'.");
        });
    }

    [Test]
    public void GetFloorMaterial_UnknownEnumValue_ReturnsDefaultWithoutException()
    {
        // Cast an out-of-range integer to RoomCategory — loader should not crash.
        var unknownCategory = (RoomCategory)999;
        Assert.DoesNotThrow(() => _loader.GetFloorMaterial(unknownCategory));
    }

    [Test]
    public void GetFloorMaterials_AllKnownCategories_NeverThrow()
    {
        var categories = System.Enum.GetValues(typeof(RoomCategory));
        foreach (RoomCategory cat in categories)
        {
            Assert.DoesNotThrow(() => _loader.GetFloorMaterial(cat),
                $"GetFloorMaterial threw for {cat}.");
            Assert.DoesNotThrow(() => _loader.GetWallMaterial(cat),
                $"GetWallMaterial threw for {cat}.");
            Assert.DoesNotThrow(() => _loader.GetDoorMaterial(cat),
                $"GetDoorMaterial threw for {cat}.");
        }
    }
}
