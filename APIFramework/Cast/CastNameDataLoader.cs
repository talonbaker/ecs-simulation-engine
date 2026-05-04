using System;
using System.IO;
using System.Text.Json;
using Warden.Contracts;

namespace APIFramework.Cast;

/// <summary>
/// Loads the cast name catalog from <c>docs/c2-content/cast/name-data.json</c>.
/// Mirrors <see cref="APIFramework.Bootstrap.NamePoolLoader"/>'s discipline:
/// fail-closed on missing required blocks; cache the default load in a <see cref="Lazy{T}"/>.
/// </summary>
public static class CastNameDataLoader
{
    private static readonly Lazy<CastNameData?> _default =
        new(() =>
        {
            var path = FindDefault();
            return path is null ? null : Load(path);
        });

    /// <summary>
    /// Loads the catalog at <paramref name="path"/>. Throws <see cref="InvalidOperationException"/>
    /// when the file is missing, malformed, or missing a required block.
    /// </summary>
    public static CastNameData Load(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"Cast name data not found: {path}");

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<CastNameData>(json, JsonOptions.Wire)
                   ?? throw new InvalidOperationException("name-data.json deserialised to null.");

        ValidateRequired(data, path);
        return data;
    }

    /// <summary>
    /// Returns the cached default catalog (loaded once from the standard path on first access),
    /// or <c>null</c> if the file cannot be located.
    /// </summary>
    public static CastNameData? LoadDefault() => _default.Value;

    /// <summary>
    /// Walks up from the current directory looking for <c>docs/c2-content/cast/name-data.json</c>.
    /// Returns <c>null</c> if not found within 8 directory levels.
    /// </summary>
    public static string? FindDefault()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 8; i++)
        {
            if (dir is null) break;
            var candidate = Path.Combine(
                dir.FullName, "docs", "c2-content", "cast", "name-data.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static void ValidateRequired(CastNameData data, string path)
    {
        if (data.FirstNames.Count == 0)
            throw new InvalidOperationException($"{path}: firstNames is required and must be non-empty.");

        foreach (var key in new[] { "male", "female", "neutral" })
        {
            if (!data.FirstNames.TryGetValue(key, out var arr) || arr.Length == 0)
                throw new InvalidOperationException($"{path}: firstNames.{key} is required and must be non-empty.");
        }

        if (data.FusionPrefixes.Length == 0)
            throw new InvalidOperationException($"{path}: fusionPrefixes is required and must be non-empty.");
        if (data.FusionSuffixes.Length == 0)
            throw new InvalidOperationException($"{path}: fusionSuffixes is required and must be non-empty.");
        if (data.StaticLastNames.Length == 0)
            throw new InvalidOperationException($"{path}: staticLastNames is required and must be non-empty.");
        if (data.LegendaryRoots.Count == 0)
            throw new InvalidOperationException($"{path}: legendaryRoots is required and must be non-empty.");

        foreach (var key in new[] { "male", "female", "neutral" })
        {
            if (!data.LegendaryRoots.TryGetValue(key, out var arr) || arr.Length == 0)
                throw new InvalidOperationException($"{path}: legendaryRoots.{key} is required and must be non-empty.");
        }

        if (data.LegendaryTitles.Length == 0)
            throw new InvalidOperationException($"{path}: legendaryTitles is required and must be non-empty.");
        if (data.CorporateTitles.Length == 0)
            throw new InvalidOperationException($"{path}: corporateTitles is required and must be non-empty.");
    }
}
