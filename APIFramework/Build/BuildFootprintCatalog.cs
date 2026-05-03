using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace APIFramework.Build;

/// <summary>
/// Immutable catalog of per-prop-type footprint defaults loaded from prop-footprints.json.
/// Modders extend the catalog by adding entries to the JSON file; no code changes needed.
/// </summary>
public sealed class BuildFootprintCatalog
{
    private readonly IReadOnlyDictionary<string, BuildFootprintEntry> _entries;

    /// <summary>All prop type ids registered in this catalog.</summary>
    public IReadOnlyList<string> AllPropTypeIds { get; }

    private BuildFootprintCatalog(
        IReadOnlyDictionary<string, BuildFootprintEntry> entries,
        IReadOnlyList<string> allIds)
    {
        _entries       = entries;
        AllPropTypeIds = allIds;
    }

    /// <summary>
    /// Returns the footprint entry for the given prop type id, or null if the type is not catalogued.
    /// </summary>
    public BuildFootprintEntry? GetByPropType(string propTypeId)
    {
        _entries.TryGetValue(propTypeId, out var entry);
        return entry;
    }

    /// <summary>Loads the catalog from the given JSON file path.</summary>
    public static BuildFootprintCatalog Load(string jsonPath)
    {
        return ParseJson(File.ReadAllText(jsonPath));
    }

    /// <summary>Parses catalog JSON directly (used for tests and in-memory bootstrapping).</summary>
    public static BuildFootprintCatalog ParseJson(string json)
    {
        var dto = JsonConvert.DeserializeObject<CatalogFileDto>(json,
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore });

        var entries = new Dictionary<string, BuildFootprintEntry>(StringComparer.OrdinalIgnoreCase);
        if (dto?.PropFootprints != null)
        {
            foreach (var entry in dto.PropFootprints)
            {
                if (string.IsNullOrEmpty(entry.PropTypeId)) continue;
                entries[entry.PropTypeId] = new BuildFootprintEntry
                {
                    PropTypeId        = entry.PropTypeId,
                    WidthTiles        = entry.WidthTiles,
                    DepthTiles        = entry.DepthTiles,
                    BottomHeight      = entry.BottomHeight,
                    TopHeight         = entry.TopHeight,
                    CanStackOnTop     = entry.CanStackOnTop,
                    FootprintCategory = entry.FootprintCategory ?? string.Empty
                };
            }
        }

        return new BuildFootprintCatalog(entries, new List<string>(entries.Keys));
    }

    /// <summary>
    /// Loads the default catalog by walking up from the working directory.
    /// Returns an empty catalog when the file cannot be found.
    /// </summary>
    public static BuildFootprintCatalog LoadDefault()
    {
        const string relPath = "docs/c2-content/build/prop-footprints.json";
        var path = FindFile(relPath);
        if (path is null)
            return Empty();

        try { return Load(path); }
        catch (Exception ex)
        {
            Console.WriteLine($"[BuildFootprintCatalog] Failed to load '{path}': {ex.Message} — using empty catalog.");
            return Empty();
        }
    }

    private static BuildFootprintCatalog Empty() =>
        new BuildFootprintCatalog(
            new Dictionary<string, BuildFootprintEntry>(),
            new List<string>());

    private static string? FindFile(string relPath)
    {
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, relPath);
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        return null;
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private class CatalogFileDto
    {
        [JsonProperty("schemaVersion")]
        public string? SchemaVersion { get; set; }

        [JsonProperty("propFootprints")]
        public List<PropFootprintEntryDto>? PropFootprints { get; set; }
    }

    private class PropFootprintEntryDto
    {
        [JsonProperty("propTypeId")]
        public string? PropTypeId { get; set; }

        [JsonProperty("widthTiles")]
        public int WidthTiles { get; set; }

        [JsonProperty("depthTiles")]
        public int DepthTiles { get; set; }

        [JsonProperty("bottomHeight")]
        public float BottomHeight { get; set; }

        [JsonProperty("topHeight")]
        public float TopHeight { get; set; }

        [JsonProperty("canStackOnTop")]
        public bool CanStackOnTop { get; set; }

        [JsonProperty("footprintCategory")]
        public string? FootprintCategory { get; set; }
    }
}
