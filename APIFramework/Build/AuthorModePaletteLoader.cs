using System;
using System.IO;
using System.Text.Json;
using Warden.Contracts;

namespace APIFramework.Build;

/// <summary>
/// Loads <c>docs/c2-content/build/author-mode-palette.json</c>. Mirrors the
/// <see cref="APIFramework.Bootstrap.NamePoolLoader"/> pattern (Lazy default cache;
/// fail-closed on missing required blocks; up-tree directory walk to discover the file).
/// </summary>
public static class AuthorModePaletteLoader
{
    private static readonly Lazy<AuthorModePaletteData?> _default =
        new(() =>
        {
            var path = FindDefault();
            return path is null ? null : Load(path);
        });

    /// <summary>
    /// Loads the palette at <paramref name="path"/>. Throws <see cref="InvalidOperationException"/>
    /// when the file is missing, malformed, or missing a required block.
    /// </summary>
    public static AuthorModePaletteData Load(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"Author-mode palette not found: {path}");

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<AuthorModePaletteData>(json, JsonOptions.Wire)
                   ?? throw new InvalidOperationException("author-mode-palette.json deserialised to null.");

        ValidateRequired(data, path);
        return data;
    }

    /// <summary>Cached default load (loaded once on first access); null if discovery failed.</summary>
    public static AuthorModePaletteData? LoadDefault() => _default.Value;

    /// <summary>Walks up looking for <c>docs/c2-content/build/author-mode-palette.json</c>.</summary>
    public static string? FindDefault()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 8; i++)
        {
            if (dir is null) break;
            var candidate = Path.Combine(
                dir.FullName, "docs", "c2-content", "build", "author-mode-palette.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static void ValidateRequired(AuthorModePaletteData data, string path)
    {
        if (data.Rooms.Length == 0)
            throw new InvalidOperationException($"{path}: rooms is required and must be non-empty.");
        if (data.LightSources.Length == 0)
            throw new InvalidOperationException($"{path}: lightSources is required and must be non-empty.");
        if (data.LightApertures.Length == 0)
            throw new InvalidOperationException($"{path}: lightApertures is required and must be non-empty.");
    }
}
