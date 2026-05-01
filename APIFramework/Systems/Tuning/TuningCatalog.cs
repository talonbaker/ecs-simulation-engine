using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using APIFramework.Components;

namespace APIFramework.Systems.Tuning;

/// <summary>
/// Centralized loader and lookup for all per-archetype tuning JSONs shipped with WP-3.2.5.
/// Loaded once at boot (via <see cref="LoadFromDirectory"/>); all getters are O(1) dictionary lookups.
/// Falls back to neutral multipliers (1.0) for any archetype not listed in the JSON.
/// </summary>
public sealed class TuningCatalog
{
    private readonly IReadOnlyDictionary<string, ChokeBias>              _choke;
    private readonly IReadOnlyDictionary<string, SlipBias>               _slip;
    private readonly IReadOnlyDictionary<string, BereavementBias>        _bereavement;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<ChoreKind, float>> _chore;
    private readonly IReadOnlyDictionary<string, RescueBias>             _rescue;
    private readonly IReadOnlyDictionary<string, float>                  _mass;
    private readonly IReadOnlyDictionary<string, MemoryPersistenceBias>  _memory;

    private TuningCatalog(
        IReadOnlyDictionary<string, ChokeBias>             choke,
        IReadOnlyDictionary<string, SlipBias>              slip,
        IReadOnlyDictionary<string, BereavementBias>       bereavement,
        IReadOnlyDictionary<string, IReadOnlyDictionary<ChoreKind, float>> chore,
        IReadOnlyDictionary<string, RescueBias>            rescue,
        IReadOnlyDictionary<string, float>                 mass,
        IReadOnlyDictionary<string, MemoryPersistenceBias> memory)
    {
        _choke       = choke;
        _slip        = slip;
        _bereavement = bereavement;
        _chore       = chore;
        _rescue      = rescue;
        _mass        = mass;
        _memory      = memory;
    }

    // ── Public getters ────────────────────────────────────────────────────────

    public ChokeBias GetChokeBias(string? archetypeId) =>
        !string.IsNullOrEmpty(archetypeId) && _choke.TryGetValue(archetypeId, out var v) ? v : ChokeBias.Default;

    public SlipBias GetSlipBias(string? archetypeId) =>
        !string.IsNullOrEmpty(archetypeId) && _slip.TryGetValue(archetypeId, out var v) ? v : SlipBias.Default;

    public BereavementBias GetBereavementBias(string? archetypeId) =>
        !string.IsNullOrEmpty(archetypeId) && _bereavement.TryGetValue(archetypeId, out var v) ? v : BereavementBias.Default;

    public float GetChoreAcceptanceBias(string? archetypeId, ChoreKind kind)
    {
        if (!string.IsNullOrEmpty(archetypeId) && _chore.TryGetValue(archetypeId, out var choreMap))
            if (choreMap.TryGetValue(kind, out var bias))
                return bias;
        return 0.50f;
    }

    public RescueBias GetRescueBias(string? archetypeId) =>
        !string.IsNullOrEmpty(archetypeId) && _rescue.TryGetValue(archetypeId, out var v) ? v : RescueBias.Default;

    /// <summary>Returns per-archetype body mass in kg. Defaults to 70 kg when the archetype is not listed.</summary>
    public float GetMassKg(string? archetypeId) =>
        !string.IsNullOrEmpty(archetypeId) && _mass.TryGetValue(archetypeId, out var kg) ? kg : 70f;

    public MemoryPersistenceBias GetMemoryPersistenceBias(string? archetypeId) =>
        !string.IsNullOrEmpty(archetypeId) && _memory.TryGetValue(archetypeId, out var v) ? v : MemoryPersistenceBias.Default;

    /// <summary>All archetype IDs present in the choke-bias table (representative for coverage checks).</summary>
    public IEnumerable<string> ArchetypeIds => _choke.Keys;

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>All-default catalog: every getter returns the neutral multiplier for its domain.</summary>
    public static TuningCatalog Empty() => new(
        new Dictionary<string, ChokeBias>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, SlipBias>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, BereavementBias>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, IReadOnlyDictionary<ChoreKind, float>>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, RescueBias>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, MemoryPersistenceBias>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Loads all seven tuning JSONs from <c>docs/c2-content/tuning/</c>, searching upward from
    /// <paramref name="tuningDir"/> (or the CWD when null). Returns a catalog with all-default
    /// multipliers for any file that is missing or malformed — the simulation never breaks on
    /// missing tuning data.
    /// </summary>
    public static TuningCatalog LoadFromDirectory(string? tuningDir = null)
    {
        tuningDir ??= FindTuningDir();

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var choke       = LoadChoke(tuningDir, opts);
        var slip        = LoadSlip(tuningDir, opts);
        var bereavement = LoadBereavement(tuningDir, opts);
        var chore       = LoadChore(tuningDir, opts);
        var rescue      = LoadRescue(tuningDir, opts);
        var mass        = LoadMass(tuningDir, opts);
        var memory      = LoadMemory(tuningDir, opts);

        return new TuningCatalog(choke, slip, bereavement, chore, rescue, mass, memory);
    }

    // ── Individual loaders ────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, ChokeBias> LoadChoke(string? dir, JsonSerializerOptions opts)
    {
        var result = new Dictionary<string, ChokeBias>(StringComparer.OrdinalIgnoreCase);
        var dto = TryLoad<ChokeBiasFileDto>(dir, "archetype-choke-bias.json", opts);
        if (dto?.ArchetypeChokeBias is null) return result;
        foreach (var e in dto.ArchetypeChokeBias)
            if (!string.IsNullOrEmpty(e.Archetype))
                result[e.Archetype] = new ChokeBias(e.BolusSizeThresholdMult, e.EnergyThresholdMult, e.StressThresholdMult);
        return result;
    }

    private static IReadOnlyDictionary<string, SlipBias> LoadSlip(string? dir, JsonSerializerOptions opts)
    {
        var result = new Dictionary<string, SlipBias>(StringComparer.OrdinalIgnoreCase);
        var dto = TryLoad<SlipBiasFileDto>(dir, "archetype-slip-bias.json", opts);
        if (dto?.ArchetypeSlipBias is null) return result;
        foreach (var e in dto.ArchetypeSlipBias)
            if (!string.IsNullOrEmpty(e.Archetype))
                result[e.Archetype] = new SlipBias(e.MovementSpeedFactor, e.SlipChanceMult);
        return result;
    }

    private static IReadOnlyDictionary<string, BereavementBias> LoadBereavement(string? dir, JsonSerializerOptions opts)
    {
        var result = new Dictionary<string, BereavementBias>(StringComparer.OrdinalIgnoreCase);
        var dto = TryLoad<BereavementBiasFileDto>(dir, "archetype-bereavement-bias.json", opts);
        if (dto?.ArchetypeBereavementBias is null) return result;
        foreach (var e in dto.ArchetypeBereavementBias)
            if (!string.IsNullOrEmpty(e.Archetype))
                result[e.Archetype] = new BereavementBias(e.StressIntensityMult, e.MoodIntensityMult, e.MemoryPersistenceMult);
        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<ChoreKind, float>> LoadChore(string? dir, JsonSerializerOptions opts)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<ChoreKind, float>>(StringComparer.OrdinalIgnoreCase);
        var dto = TryLoad<ChoreAcceptanceBiasFileDto>(dir, "archetype-chore-acceptance-bias.json", opts);
        if (dto?.Biases is null) return result;

        foreach (var (archetypeId, rawMap) in dto.Biases)
        {
            var choreMap = new Dictionary<ChoreKind, float>();
            foreach (var (choreName, value) in rawMap)
                if (ChoreNameMap.TryGetValue(choreName, out var kind))
                    choreMap[kind] = value;
            result[archetypeId] = choreMap;
        }
        return result;
    }

    private static IReadOnlyDictionary<string, RescueBias> LoadRescue(string? dir, JsonSerializerOptions opts)
    {
        var result = new Dictionary<string, RescueBias>(StringComparer.OrdinalIgnoreCase);
        var dto = TryLoad<RescueBiasFileDto>(dir, "archetype-rescue-bias.json", opts);
        if (dto?.ArchetypeRescueBias is null) return result;
        foreach (var e in dto.ArchetypeRescueBias)
            if (!string.IsNullOrEmpty(e.Archetype))
                result[e.Archetype] = new RescueBias(
                    Math.Clamp(e.Bias, 0f, 1f),
                    Math.Clamp(e.HeimlichCompetence, 0f, 1f),
                    Math.Clamp(e.CprCompetence, 0f, 1f),
                    Math.Clamp(e.DoorUnlockCompetence, 0f, 1f));
        return result;
    }

    private static IReadOnlyDictionary<string, float> LoadMass(string? dir, JsonSerializerOptions opts)
    {
        var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var dto = TryLoad<PhysicsMassFileDto>(dir, "archetype-physics-mass.json", opts);
        if (dto?.ArchetypeMass is null) return result;
        foreach (var e in dto.ArchetypeMass)
            if (!string.IsNullOrEmpty(e.Archetype))
                result[e.Archetype] = Math.Clamp(e.MassKg, 1f, 500f);
        return result;
    }

    private static IReadOnlyDictionary<string, MemoryPersistenceBias> LoadMemory(string? dir, JsonSerializerOptions opts)
    {
        var result = new Dictionary<string, MemoryPersistenceBias>(StringComparer.OrdinalIgnoreCase);
        var dto = TryLoad<MemoryPersistenceBiasFileDto>(dir, "archetype-memory-persistence-bias.json", opts);
        if (dto?.ArchetypeMemoryPersistenceBias is null) return result;
        foreach (var e in dto.ArchetypeMemoryPersistenceBias)
            if (!string.IsNullOrEmpty(e.Archetype))
                result[e.Archetype] = new MemoryPersistenceBias(
                    Math.Clamp(e.PersistenceMult, 0.5f, 2f),
                    Math.Clamp(e.DecayRateMult, 0.5f, 2f));
        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static T? TryLoad<T>(string? dir, string filename, JsonSerializerOptions opts)
    {
        if (dir is null) return default;
        var path = Path.Combine(dir, filename);
        if (!File.Exists(path)) return default;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, opts);
        }
        catch
        {
            return default;
        }
    }

    private static string? FindTuningDir()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 8; i++)
        {
            if (dir is null) break;
            var candidate = Path.Combine(dir.FullName, "docs", "c2-content", "tuning");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static readonly IReadOnlyDictionary<string, ChoreKind> ChoreNameMap =
        new Dictionary<string, ChoreKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["cleanMicrowave"]      = ChoreKind.CleanMicrowave,
            ["cleanFridge"]         = ChoreKind.CleanFridge,
            ["cleanBathroom"]       = ChoreKind.CleanBathroom,
            ["takeOutTrash"]        = ChoreKind.TakeOutTrash,
            ["refillWaterCooler"]   = ChoreKind.RefillWaterCooler,
            ["restockSupplyCloset"] = ChoreKind.RestockSupplyCloset,
            ["replaceToner"]        = ChoreKind.ReplaceToner,
        };

    // ── DTOs ─────────────────────────────────────────────────────────────────

    private sealed class ChokeBiasFileDto
    {
        [JsonPropertyName("archetypeChokeBias")]
        public List<ChokeBiasEntryDto> ArchetypeChokeBias { get; set; } = new();
    }
    private sealed class ChokeBiasEntryDto
    {
        [JsonPropertyName("archetype")]           public string Archetype { get; set; } = string.Empty;
        [JsonPropertyName("bolusSizeThresholdMult")] public float BolusSizeThresholdMult { get; set; } = 1f;
        [JsonPropertyName("energyThresholdMult")] public float EnergyThresholdMult { get; set; } = 1f;
        [JsonPropertyName("stressThresholdMult")] public float StressThresholdMult { get; set; } = 1f;
    }

    private sealed class SlipBiasFileDto
    {
        [JsonPropertyName("archetypeSlipBias")]
        public List<SlipBiasEntryDto> ArchetypeSlipBias { get; set; } = new();
    }
    private sealed class SlipBiasEntryDto
    {
        [JsonPropertyName("archetype")]           public string Archetype { get; set; } = string.Empty;
        [JsonPropertyName("movementSpeedFactor")] public float MovementSpeedFactor { get; set; } = 1f;
        [JsonPropertyName("slipChanceMult")]      public float SlipChanceMult { get; set; } = 1f;
    }

    private sealed class BereavementBiasFileDto
    {
        [JsonPropertyName("archetypeBereavementBias")]
        public List<BereavementBiasEntryDto> ArchetypeBereavementBias { get; set; } = new();
    }
    private sealed class BereavementBiasEntryDto
    {
        [JsonPropertyName("archetype")]             public string Archetype { get; set; } = string.Empty;
        [JsonPropertyName("stressIntensityMult")]   public float StressIntensityMult { get; set; } = 1f;
        [JsonPropertyName("moodIntensityMult")]     public float MoodIntensityMult { get; set; } = 1f;
        [JsonPropertyName("memoryPersistenceMult")] public float MemoryPersistenceMult { get; set; } = 1f;
    }

    private sealed class ChoreAcceptanceBiasFileDto
    {
        [JsonPropertyName("biases")]
        public Dictionary<string, Dictionary<string, float>>? Biases { get; set; }
    }

    private sealed class RescueBiasFileDto
    {
        [JsonPropertyName("archetypeRescueBias")]
        public List<RescueBiasEntryDto> ArchetypeRescueBias { get; set; } = new();
    }
    private sealed class RescueBiasEntryDto
    {
        [JsonPropertyName("archetype")]           public string Archetype { get; set; } = string.Empty;
        [JsonPropertyName("bias")]                public float Bias { get; set; }
        [JsonPropertyName("heimlichCompetence")]  public float HeimlichCompetence { get; set; }
        [JsonPropertyName("cprCompetence")]       public float CprCompetence { get; set; }
        [JsonPropertyName("doorUnlockCompetence")] public float DoorUnlockCompetence { get; set; }
    }

    private sealed class PhysicsMassFileDto
    {
        [JsonPropertyName("archetypeMass")]
        public List<PhysicsMassEntryDto> ArchetypeMass { get; set; } = new();
    }
    private sealed class PhysicsMassEntryDto
    {
        [JsonPropertyName("archetype")] public string Archetype { get; set; } = string.Empty;
        [JsonPropertyName("massKg")]    public float MassKg { get; set; } = 70f;
    }

    private sealed class MemoryPersistenceBiasFileDto
    {
        [JsonPropertyName("archetypeMemoryPersistenceBias")]
        public List<MemoryPersistenceBiasEntryDto> ArchetypeMemoryPersistenceBias { get; set; } = new();
    }
    private sealed class MemoryPersistenceBiasEntryDto
    {
        [JsonPropertyName("archetype")]        public string Archetype { get; set; } = string.Empty;
        [JsonPropertyName("persistenceMult")]  public float PersistenceMult { get; set; } = 1f;
        [JsonPropertyName("decayRateMult")]    public float DecayRateMult { get; set; } = 1f;
    }
}

// ── Bias value structs ────────────────────────────────────────────────────────

/// <summary>Per-archetype multipliers applied to ChokingDetectionSystem thresholds.</summary>
public readonly record struct ChokeBias(float BolusSizeThresholdMult, float EnergyThresholdMult, float StressThresholdMult)
{
    public static readonly ChokeBias Default = new(1f, 1f, 1f);
}

/// <summary>Per-archetype multipliers applied to SlipAndFallSystem slip-chance computation.</summary>
public readonly record struct SlipBias(float MovementSpeedFactor, float SlipChanceMult)
{
    public static readonly SlipBias Default = new(1f, 1f);
}

/// <summary>Per-archetype multipliers applied to BereavementSystem stress and grief calculations.</summary>
public readonly record struct BereavementBias(float StressIntensityMult, float MoodIntensityMult, float MemoryPersistenceMult)
{
    public static readonly BereavementBias Default = new(1f, 1f, 1f);
}

/// <summary>Per-archetype rescue likelihood and competence bonuses.</summary>
public readonly record struct RescueBias(float Bias, float HeimlichCompetence, float CprCompetence, float DoorUnlockCompetence)
{
    public static readonly RescueBias Default = new(0f, 0f, 0f, 0f);
}

/// <summary>Per-archetype memory persistence and decay multipliers applied in MemoryRecordingSystem.</summary>
public readonly record struct MemoryPersistenceBias(float PersistenceMult, float DecayRateMult)
{
    public static readonly MemoryPersistenceBias Default = new(1f, 1f);
}
