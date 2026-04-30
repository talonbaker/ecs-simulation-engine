using System;
using System.IO;
using System.Text.Json;
using Warden.Contracts;

namespace APIFramework.Bootstrap;

/// <summary>
/// Deserialization target for <c>docs/c2-content/cast/name-pool.json</c>.
/// </summary>
public sealed class NamePoolDto
{
    /// <summary>Schema version string declared by the JSON file (informational only).</summary>
    public string   SchemaVersion { get; set; } = "";

    /// <summary>Pool of first names available for cast generation; sampled without replacement at spawn time.</summary>
    public string[] FirstNames    { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Loads the NPC first-name pool from <c>docs/c2-content/cast/name-pool.json</c>.
/// The default load is cached in a <see cref="Lazy{T}"/> so the file is read once per process.
/// </summary>
public static class NamePoolLoader
{
    private static readonly Lazy<NamePoolDto?> _default =
        new(() =>
        {
            var path = FindDefault();
            return path is null ? null : Load(path);
        });

    /// <summary>
    /// Loads a name pool from the given <paramref name="path"/>.
    /// Throws <see cref="InvalidOperationException"/> if the file is missing or malformed.
    /// </summary>
    /// <param name="path">Absolute or relative path to a name-pool JSON file.</param>
    /// <returns>The deserialised <see cref="NamePoolDto"/>.</returns>
    public static NamePoolDto Load(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"Name pool not found: {path}");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<NamePoolDto>(json, JsonOptions.Wire)
               ?? throw new InvalidOperationException("name-pool.json deserialised to null.");
    }

    /// <summary>
    /// Returns the cached default pool (loaded once from the standard path on first access),
    /// or <c>null</c> if the file cannot be located.
    /// </summary>
    /// <returns>The cached <see cref="NamePoolDto"/>, or <c>null</c> if no file was discovered.</returns>
    public static NamePoolDto? LoadDefault() => _default.Value;

    /// <summary>
    /// Walks up from the current directory looking for
    /// <c>docs/c2-content/cast/name-pool.json</c>.
    /// Returns <c>null</c> if not found within 8 directory levels.
    /// </summary>
    /// <returns>The discovered absolute path, or <c>null</c> when no candidate exists.</returns>
    public static string? FindDefault()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 8; i++)
        {
            if (dir is null) break;
            var candidate = Path.Combine(
                dir.FullName, "docs", "c2-content", "cast", "name-pool.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
