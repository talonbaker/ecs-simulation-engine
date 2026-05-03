using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems.Spatial;

// ── Deserialization DTOs ──────────────────────────────────────────────────────

internal sealed class PersonalSpaceCatalogDto
{
    public string SchemaVersion { get; set; } = "";

    [JsonPropertyName("archetypePersonalSpace")]
    public PersonalSpaceEntryDto[] ArchetypePersonalSpace { get; set; } = Array.Empty<PersonalSpaceEntryDto>();
}

internal sealed class PersonalSpaceEntryDto
{
    [JsonPropertyName("archetype")]
    public string Archetype { get; set; } = "";

    [JsonPropertyName("radiusMult")]
    public float RadiusMult { get; set; } = 1.0f;

    [JsonPropertyName("repulsionStrengthMult")]
    public float RepulsionStrengthMult { get; set; } = 1.0f;
}

// ── System ────────────────────────────────────────────────────────────────────

/// <summary>
/// PreUpdate phase. Attaches <see cref="PersonalSpaceComponent"/> to every NPC that
/// lacks one. Defaults are 0.6 m radius / 0.3 strength; per-archetype multipliers
/// from <c>archetype-personal-space.json</c> are applied when available.
/// </summary>
/// <remarks>
/// Reads: <see cref="NpcTag"/>, <see cref="NpcArchetypeComponent"/>.<br/>
/// Writes: <see cref="PersonalSpaceComponent"/> (single writer at attach time; idempotent).<br/>
/// Phase: PreUpdate, before any system that reads PersonalSpaceComponent.
/// </remarks>
public sealed class SpatialBehaviorInitializerSystem : ISystem
{
    private const float DefaultRadius   = 0.6f;
    private const float DefaultStrength = 0.3f;

    private readonly IReadOnlyDictionary<string, (float RadiusMult, float StrengthMult)> _tuning;

    /// <summary>Constructs the initializer with a pre-loaded per-archetype tuning table.</summary>
    public SpatialBehaviorInitializerSystem(
        IReadOnlyDictionary<string, (float, float)> tuning)
    {
        _tuning = tuning;
    }

    /// <summary>Per-tick idempotent attach pass.</summary>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<NpcTag>())
        {
            if (entity.Has<PersonalSpaceComponent>()) continue;

            float radius   = DefaultRadius;
            float strength = DefaultStrength;

            if (entity.Has<NpcArchetypeComponent>())
            {
                var archetypeId = entity.Get<NpcArchetypeComponent>().ArchetypeId;
                if (_tuning.TryGetValue(archetypeId, out var mults))
                {
                    radius   = Math.Clamp(DefaultRadius   * mults.RadiusMult,   0.1f, 5.0f);
                    strength = Math.Clamp(DefaultStrength * mults.StrengthMult, 0.0f, 1.0f);
                }
            }

            entity.Add(new PersonalSpaceComponent
            {
                RadiusMeters     = radius,
                RepulsionStrength = strength
            });
        }
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Loads per-archetype tuning from the given JSON path, or searches upward from
    /// the CWD for the default file. Returns an empty table when not found.
    /// </summary>
    public static IReadOnlyDictionary<string, (float RadiusMult, float StrengthMult)> LoadTuning(
        string? path = null)
    {
        path ??= FindDefaultPath();
        if (path is null || !File.Exists(path))
            return new Dictionary<string, (float, float)>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto  = JsonSerializer.Deserialize<PersonalSpaceCatalogDto>(json, opts);
            if (dto?.ArchetypePersonalSpace is null)
                return new Dictionary<string, (float, float)>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, (float, float)>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in dto.ArchetypePersonalSpace)
            {
                if (string.IsNullOrEmpty(entry.Archetype)) continue;
                float rMult = Math.Clamp(entry.RadiusMult,           0.5f, 2.0f);
                float sMult = Math.Clamp(entry.RepulsionStrengthMult, 0.5f, 2.0f);
                result[entry.Archetype] = (rMult, sMult);
            }
            return result;
        }
        catch
        {
            return new Dictionary<string, (float, float)>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Walks up from the CWD to locate the default tuning file.</summary>
    public static string? FindDefaultPath()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 8; i++)
        {
            if (dir is null) break;
            var candidate = Path.Combine(
                dir.FullName, "docs", "c2-content", "archetypes",
                "archetype-personal-space.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
