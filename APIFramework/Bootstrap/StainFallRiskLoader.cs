using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Warden.Contracts;

namespace APIFramework.Bootstrap;

/// <summary>
/// Deserialization target for <c>docs/c2-content/hazards/stain-fall-risk.json</c>.
/// </summary>
public sealed class StainFallRiskDto
{
    public string SchemaVersion { get; set; } = "";
    public List<StainKindRiskDto> StainKindFallRisk { get; set; } = new();
}

/// <summary>
/// A single stain kind and its default fall-risk level.
/// </summary>
public sealed class StainKindRiskDto
{
    public string Kind { get; set; } = "";
    public float FallRiskLevel { get; set; } = 0f;
}

/// <summary>
/// Loads the stain fall-risk catalog from <c>docs/c2-content/hazards/stain-fall-risk.json</c>.
/// The default load is cached in a <see cref="Lazy{T}"/> so the file is read once per process.
/// </summary>
public static class StainFallRiskLoader
{
    private static readonly Lazy<StainFallRiskDto?> _default =
        new(() =>
        {
            var path = FindDefault();
            return path is null ? null : Load(path);
        });

    /// <summary>
    /// Loads a stain fall-risk catalog from the given <paramref name="path"/>.
    /// Throws <see cref="InvalidOperationException"/> if the file is missing or malformed.
    /// </summary>
    public static StainFallRiskDto Load(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"Stain fall-risk catalog not found: {path}");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<StainFallRiskDto>(json, JsonOptions.Wire)
               ?? throw new InvalidOperationException("stain-fall-risk.json deserialised to null.");
    }

    /// <summary>
    /// Returns the cached default catalog (loaded once from the standard path on first access),
    /// or <c>null</c> if the file cannot be located.
    /// </summary>
    public static StainFallRiskDto? LoadDefault() => _default.Value;

    /// <summary>
    /// Walks up from the current directory looking for
    /// <c>docs/c2-content/hazards/stain-fall-risk.json</c>.
    /// Returns <c>null</c> if not found within 8 directory levels.
    /// </summary>
    public static string? FindDefault()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 8; i++)
        {
            if (dir is null) break;
            var candidate = Path.Combine(
                dir.FullName, "docs", "c2-content", "hazards", "stain-fall-risk.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// Gets the fall-risk level for a stain kind, or 0 if not in the catalog.
    /// </summary>
    public static float GetFallRiskForKind(string kind)
    {
        var catalog = LoadDefault();
        if (catalog is null || catalog.StainKindFallRisk is null)
            return 0f;

        var entry = catalog.StainKindFallRisk.FirstOrDefault(x => x.Kind == kind);
        return entry?.FallRiskLevel ?? 0f;
    }
}
