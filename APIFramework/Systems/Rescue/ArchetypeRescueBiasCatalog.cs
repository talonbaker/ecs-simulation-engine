using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace APIFramework.Systems.Rescue;

/// <summary>
/// Loads per-archetype rescue likelihood and rescue-kind competence bonuses from
/// docs/c2-content/rescue/archetype-rescue-bias.json.
///
/// Usage (in SimulationBootstrapper):
///   var catalog = ArchetypeRescueBiasCatalog.LoadFromFile();
///   var sys = new RescueIntentSystem(catalog, ...);
///
/// When the file is missing or malformed, all lookups return 0.0 — NPCs still
/// rescue but with base-rate-only scores and no competence bonuses.
/// </summary>
public sealed class ArchetypeRescueBiasCatalog
{
    private readonly IReadOnlyDictionary<string, float> _bias;
    private readonly IReadOnlyDictionary<string, ArchetypeRescueCompetence> _competence;

    private ArchetypeRescueBiasCatalog(
        IReadOnlyDictionary<string, float> bias,
        IReadOnlyDictionary<string, ArchetypeRescueCompetence> competence)
    {
        _bias       = bias;
        _competence = competence;
    }

    /// <summary>Returns the rescue willingness bias for the archetype, or 0 if unknown.</summary>
    public float GetBias(string archetypeId)
    {
        if (string.IsNullOrEmpty(archetypeId)) return 0f;
        return _bias.TryGetValue(archetypeId, out var v) ? v : 0f;
    }

    /// <summary>Returns the competence bonus for the archetype and rescue kind, or 0 if unknown.</summary>
    public float GetCompetence(string archetypeId, RescueKind kind)
    {
        if (string.IsNullOrEmpty(archetypeId)) return 0f;
        if (!_competence.TryGetValue(archetypeId, out var comp)) return 0f;
        return kind switch
        {
            RescueKind.Heimlich   => comp.Heimlich,
            RescueKind.CPR        => comp.Cpr,
            RescueKind.DoorUnlock => comp.DoorUnlock,
            _                     => 0f,
        };
    }

    /// <summary>
    /// Loads the catalog from <paramref name="path"/>, or searches upward from the CWD
    /// for docs/c2-content/rescue/archetype-rescue-bias.json when path is null.
    /// Returns a zero-bias catalog when the file cannot be found or parsed.
    /// </summary>
    public static ArchetypeRescueBiasCatalog LoadFromFile(string? path = null)
    {
        path ??= FindDefaultPath();
        if (path is null || !File.Exists(path))
            return Empty();

        try
        {
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto  = JsonSerializer.Deserialize<RescueBiasCatalogDto>(json, opts);
            if (dto?.ArchetypeRescueBias is null) return Empty();

            var bias       = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var competence = new Dictionary<string, ArchetypeRescueCompetence>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in dto.ArchetypeRescueBias)
            {
                if (string.IsNullOrEmpty(entry.Archetype)) continue;
                bias[entry.Archetype]       = Math.Clamp(entry.Bias, 0f, 1f);
                competence[entry.Archetype] = new ArchetypeRescueCompetence(
                    Math.Clamp(entry.HeimlichCompetence, 0f, 1f),
                    Math.Clamp(entry.CprCompetence,      0f, 1f),
                    Math.Clamp(entry.DoorUnlockCompetence, 0f, 1f)
                );
            }

            return new ArchetypeRescueBiasCatalog(bias, competence);
        }
        catch
        {
            return Empty();
        }
    }

    /// <summary>Returns all archetype IDs present in this catalog.</summary>
    public IEnumerable<string> ArchetypeIds => _bias.Keys;

    private static ArchetypeRescueBiasCatalog Empty() =>
        new(
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, ArchetypeRescueCompetence>(StringComparer.OrdinalIgnoreCase)
        );

    private static string? FindDefaultPath()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 8; i++)
        {
            if (dir is null) break;
            var candidate = Path.Combine(dir.FullName, "docs", "c2-content", "rescue",
                "archetype-rescue-bias.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed class RescueBiasCatalogDto
    {
        [JsonPropertyName("archetypeRescueBias")]
        public List<RescueBiasEntryDto> ArchetypeRescueBias { get; set; } = new();
    }

    private sealed class RescueBiasEntryDto
    {
        [JsonPropertyName("archetype")]
        public string Archetype { get; set; } = string.Empty;

        [JsonPropertyName("bias")]
        public float Bias { get; set; }

        [JsonPropertyName("heimlichCompetence")]
        public float HeimlichCompetence { get; set; }

        [JsonPropertyName("cprCompetence")]
        public float CprCompetence { get; set; }

        [JsonPropertyName("doorUnlockCompetence")]
        public float DoorUnlockCompetence { get; set; }
    }
}

/// <summary>Per-archetype competence bonuses that add to the base success rate for each RescueKind.</summary>
public readonly record struct ArchetypeRescueCompetence(float Heimlich, float Cpr, float DoorUnlock);
