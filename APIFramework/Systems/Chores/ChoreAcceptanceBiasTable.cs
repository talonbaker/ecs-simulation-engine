using System;
using System.Collections.Generic;
using System.IO;
using APIFramework.Components;
using Newtonsoft.Json;

namespace APIFramework.Systems.Chores;

/// <summary>
/// Immutable lookup table loaded from chore-archetype-acceptance-bias.json.
/// Provides acceptance-bias floats keyed by (archetypeId, ChoreKind).
/// </summary>
public sealed class ChoreAcceptanceBiasTable
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<ChoreKind, float>> _biases;
    private readonly float _defaultBias;

    private static readonly IReadOnlyDictionary<string, ChoreKind> ChoreNameMap =
        new Dictionary<string, ChoreKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["cleanMicrowave"]     = ChoreKind.CleanMicrowave,
            ["cleanFridge"]        = ChoreKind.CleanFridge,
            ["cleanBathroom"]      = ChoreKind.CleanBathroom,
            ["takeOutTrash"]       = ChoreKind.TakeOutTrash,
            ["refillWaterCooler"]  = ChoreKind.RefillWaterCooler,
            ["restockSupplyCloset"]= ChoreKind.RestockSupplyCloset,
            ["replaceToner"]       = ChoreKind.ReplaceToner,
        };

    public ChoreAcceptanceBiasTable(
        IReadOnlyDictionary<string, IReadOnlyDictionary<ChoreKind, float>> biases,
        float defaultBias = 0.50f)
    {
        _biases      = biases;
        _defaultBias = defaultBias;
    }

    /// <summary>
    /// Returns the acceptance bias for the given archetype and chore kind.
    /// Falls back to <c>defaultBias</c> when the archetype or chore kind is not listed.
    /// </summary>
    public float GetBias(string archetypeId, ChoreKind kind)
    {
        if (archetypeId != null && _biases.TryGetValue(archetypeId, out var choreMap))
            if (choreMap.TryGetValue(kind, out var bias))
                return bias;
        return _defaultBias;
    }

    /// <summary>Loads the default bias file, searching upward from CWD (max 6 levels).</summary>
    public static ChoreAcceptanceBiasTable LoadDefault(float defaultBias = 0.50f)
    {
        const string relPath = "docs/c2-content/chores/chore-archetype-acceptance-bias.json";
        var path = FindFile(relPath);
        if (path is null) return new ChoreAcceptanceBiasTable(
            new Dictionary<string, IReadOnlyDictionary<ChoreKind, float>>(), defaultBias);

        try
        {
            var json = File.ReadAllText(path);
            return ParseJson(json, defaultBias);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChoreAcceptanceBiasTable] Failed to load '{path}': {ex.Message} — using defaults.");
            return new ChoreAcceptanceBiasTable(
                new Dictionary<string, IReadOnlyDictionary<ChoreKind, float>>(), defaultBias);
        }
    }

    /// <summary>Parses bias JSON directly (used for tests and bootstrapping).</summary>
    public static ChoreAcceptanceBiasTable ParseJson(string json, float defaultBias = 0.50f)
    {
        var dto = JsonConvert.DeserializeObject<BiasFileDto>(json,
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore });

        var result = new Dictionary<string, IReadOnlyDictionary<ChoreKind, float>>(StringComparer.OrdinalIgnoreCase);
        if (dto?.Biases != null)
        {
            foreach (var (archetypeId, rawMap) in dto.Biases)
            {
                var choreMap = new Dictionary<ChoreKind, float>();
                foreach (var (choreName, value) in rawMap)
                    if (ChoreNameMap.TryGetValue(choreName, out var kind))
                        choreMap[kind] = (float)value;
                result[archetypeId] = choreMap;
            }
        }
        return new ChoreAcceptanceBiasTable(result, defaultBias);
    }

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

    // ── DTO ───────────────────────────────────────────────────────────────────

    private class BiasFileDto
    {
        [JsonProperty("biases")]
        public Dictionary<string, Dictionary<string, double>>? Biases { get; set; }
    }
}
