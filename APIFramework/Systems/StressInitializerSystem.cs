using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

// ── Deserialization DTOs ──────────────────────────────────────────────────────

/// <summary>Top-level deserialization shape for archetype-stress-baselines.json.</summary>
internal sealed class StressBaselinesDto
{
    /// <summary>Per-archetype baseline entries.</summary>
    public StressBaselineEntryDto[] Archetypes { get; set; } = Array.Empty<StressBaselineEntryDto>();
}

/// <summary>Single archetype baseline entry deserialized from JSON.</summary>
internal sealed class StressBaselineEntryDto
{
    /// <summary>Archetype identifier the baseline applies to.</summary>
    public string ArchetypeId  { get; set; } = "";
    /// <summary>Starting <see cref="StressComponent.ChronicLevel"/> (0–100) for the archetype.</summary>
    public double ChronicLevel { get; set; } = 0;
}

// ── System ────────────────────────────────────────────────────────────────────

/// <summary>
/// PreUpdate phase. Attaches <see cref="StressComponent"/> to every NPC that has
/// <see cref="NpcArchetypeComponent"/> but no stress state yet. Sets
/// <see cref="StressComponent.ChronicLevel"/> from the archetype baselines lookup;
/// AcuteLevel starts at 0.
/// </summary>
/// <remarks>
/// Reads: <see cref="NpcTag"/>, <see cref="NpcArchetypeComponent"/>.<br/>
/// Writes: <see cref="StressComponent"/> (single writer at attach time; idempotent).<br/>
/// Phase: PreUpdate, before any system that reads stress state.
/// </remarks>
public class StressInitializerSystem : ISystem
{
    private readonly IReadOnlyDictionary<string, double> _baselines;

    /// <summary>Constructs the initializer with a pre-loaded baselines dictionary.</summary>
    /// <param name="baselines">Map of archetype id → starting ChronicLevel (0–100).</param>
    public StressInitializerSystem(IReadOnlyDictionary<string, double> baselines)
    {
        _baselines = baselines;
    }

    /// <summary>Per-tick idempotent attach pass.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds, unused).</param>
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
