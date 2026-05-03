using System.IO;
using APIFramework.Systems.Visual;
using Xunit;

namespace APIFramework.Tests.Visual;

/// <summary>
/// AT-02 — All 10 enum values have catalog entries pointing at valid VFX Graph asset paths.
/// Validates the JSON mirror at docs/c2-content/visual/particle-trigger-catalog.json.
/// </summary>
public class ParticleTriggerCatalogJsonTests
{
    private static string? CatalogJsonPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "docs")))
            dir = dir.Parent;

        return dir != null
            ? Path.Combine(dir.FullName, "docs", "c2-content", "visual", "particle-trigger-catalog.json")
            : null;
    }

    [Fact]
    public void CatalogJson_ExistsOnDisk()
    {
        var path = CatalogJsonPath();
        Assert.True(path != null && File.Exists(path),
            $"particle-trigger-catalog.json not found (searched from {AppContext.BaseDirectory})");
    }

    [Fact]
    public void CatalogJson_ContainsAllTenKinds()
    {
        var path = CatalogJsonPath();
        if (path == null || !File.Exists(path)) return;

        var json     = File.ReadAllText(path);
        var allKinds = System.Enum.GetNames(typeof(ParticleTriggerKind));

        foreach (var kind in allKinds)
            Assert.Contains($"\"kind\": \"{kind}\"", json);
    }

    [Fact]
    public void CatalogJson_EachEntryHasVfxAssetPath()
    {
        var path = CatalogJsonPath();
        if (path == null || !File.Exists(path)) return;

        var json     = File.ReadAllText(path);
        var allKinds = System.Enum.GetNames(typeof(ParticleTriggerKind));
        foreach (var kind in allKinds)
            Assert.Contains($"\"vfxAsset\": \"VFX/{kind}.vfx\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogJson_SchemaVersionPresent()
    {
        var path = CatalogJsonPath();
        if (path == null || !File.Exists(path)) return;
        Assert.Contains("schemaVersion", File.ReadAllText(path));
    }
}
