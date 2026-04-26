using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

// ── Deserialization DTOs ──────────────────────────────────────────────────────

internal sealed class StressBaselinesDto
{
    public StressBaselineEntryDto[] Archetypes { get; set; } = Array.Empty<StressBaselineEntryDto>();
}

internal sealed class StressBaselineEntryDto
{
    public string ArchetypeId  { get; set; } = "";
    public double ChronicLevel { get; set; } = 0;
}

// ── System ────────────────────────────────────────────────────────────────────

/// <summary>
/// Attaches <see cref="StressComponent"/> to every NPC that has
/// <see cref="NpcArchetypeComponent"/> but no stress state yet.
/// Sets ChronicLevel from the archetype-stress-baselines.json lookup; AcuteLevel starts at 0.
/// Phase: PreUpdate — runs before any system that reads stress state.
/// Idempotent: entities that already have <see cref="StressComponent"/> are skipped.
/// </summary>
public class StressInitializerSystem : ISystem
{
    private readonly IReadOnlyDictionary<string, double> _baselines;

    public StressInitializerSystem(IReadOnlyDictionary<string, double> baselines)
    {
        _baselines = baselines;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<NpcTag>().ToList())
        {
            if (!entity.Has<NpcArchetypeComponent>()) continue;
            if (entity.Has<StressComponent>()) continue;

            var archetypeId = entity.Get<NpcArchetypeComponent>().ArchetypeId;
            double chronicBaseline = _baselines.TryGetValue(archetypeId, out var v) ? v : 0.0;

            entity.Add(new StressComponent
            {
                AcuteLevel                = 0,
                ChronicLevel              = Math.Clamp(chronicBaseline, 0, 100),
                LastDayUpdated            = 0,
                SuppressionEventsToday    = 0,
                DriveSpikeEventsToday     = 0,
                SocialConflictEventsToday = 0,
                BurnoutLastAppliedDay     = 0,
            });
        }
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the archetype stress baselines dictionary from the given JSON path,
    /// or searches upward from the CWD for the default file.
    /// Returns an empty dictionary when the file cannot be found or parsed.
    /// </summary>
    public static IReadOnlyDictionary<string, double> LoadBaselines(string? path = null)
    {
        path ??= FindDefaultPath();
        if (path is null || !File.Exists(path))
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto  = JsonSerializer.Deserialize<StressBaselinesDto>(json, opts);
            if (dto is null) return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in dto.Archetypes)
                result[entry.ArchetypeId] = Math.Clamp(entry.ChronicLevel, 0, 100);
            return result;
        }
        catch
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Walks up from the CWD to locate the default baselines file.</summary>
    public static string? FindDefaultPath()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 8; i++)
        {
            if (dir is null) break;
            var candidate = Path.Combine(
                dir.FullName, "docs", "c2-content", "archetypes",
                "archetype-stress-baselines.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
