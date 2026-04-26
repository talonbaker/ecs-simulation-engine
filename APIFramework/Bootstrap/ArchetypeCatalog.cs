using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using APIFramework.Components;
using Warden.Contracts;
using Warden.Contracts.SchemaValidation;

namespace APIFramework.Bootstrap;

// ── DTOs (deserialization targets) ────────────────────────────────────────────

public sealed class ArchetypeCatalogDto
{
    public string         SchemaVersion { get; set; } = "";
    public ArchetypeDto[] Archetypes    { get; set; } = Array.Empty<ArchetypeDto>();
}

public sealed class ArchetypeDto
{
    public string   Id          { get; set; } = "";
    public string   DisplayName { get; set; } = "";

    [JsonPropertyName("chronicallyElevatedDrives")]
    public string[] ElevatedDrives  { get; set; } = Array.Empty<string>();

    [JsonPropertyName("chronicallyDepressedDrives")]
    public string[] DepressedDrives { get; set; } = Array.Empty<string>();

    public PersonalityRangesDto   PersonalityRanges    { get; set; } = new();
    public JsonElement            VocabularyRegister   { get; set; }   // string or string[]
    public int[]                  WillpowerBaselineRange { get; set; } = new[] { 40, 60 };
    public string[]               DealOptions          { get; set; } = Array.Empty<string>();
    public SilhouetteFamilyDto    SilhouetteFamily     { get; set; } = new();
    public StarterInhibitionDto[] StarterInhibitions   { get; set; } = Array.Empty<StarterInhibitionDto>();
    public RelationshipSpawnHintsDto? RelationshipSpawnHints { get; set; }
    public DialogHintsDto?        DialogHints          { get; set; }

    /// <summary>Returns the vocabulary register(s) as a string array. Single strings become a one-element array.</summary>
    public string[] GetRegisters()
    {
        if (VocabularyRegister.ValueKind == JsonValueKind.String)
            return new[] { VocabularyRegister.GetString() ?? "casual" };
        if (VocabularyRegister.ValueKind == JsonValueKind.Array)
            return VocabularyRegister.EnumerateArray()
                .Select(e => e.GetString() ?? "casual")
                .ToArray();
        return new[] { "casual" };
    }
}

public sealed class PersonalityRangesDto
{
    public int[]? Openness          { get; set; }
    public int[]? Conscientiousness { get; set; }
    public int[]? Extraversion      { get; set; }
    public int[]? Agreeableness     { get; set; }
    public int[]? Neuroticism       { get; set; }
}

public sealed class SilhouetteFamilyDto
{
    public string[] Heights          { get; set; } = Array.Empty<string>();
    public string[] Builds           { get; set; } = Array.Empty<string>();
    public string[] Hair             { get; set; } = Array.Empty<string>();
    public string[] Headwear         { get; set; } = Array.Empty<string>();
    public string[] DominantColors   { get; set; } = Array.Empty<string>();
    public string[] DistinctiveItems { get; set; } = Array.Empty<string>();
}

public sealed class StarterInhibitionDto
{
    [JsonPropertyName("class")]
    public string Class         { get; set; } = "";
    public int[]  StrengthRange { get; set; } = new[] { 0, 100 };
    public string Awareness     { get; set; } = "hidden";
}

public sealed class RelationshipSpawnHintsDto
{
    public string   Pattern                      { get; set; } = "";
    public string[] TargetArchetypePreferences   { get; set; } = Array.Empty<string>();
    public bool     IsTarget                     { get; set; }
}

public sealed class DialogHintsDto
{
    public string[] CalcifyPriorityTags { get; set; } = Array.Empty<string>();
}

// ── ArchetypeCatalog ───────────────────────────────────────────────────────────

/// <summary>
/// Loads, validates, and exposes the archetype catalog from
/// <c>docs/c2-content/archetypes/archetypes.json</c>.
/// Fail-closed: any validation or parse error throws <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class ArchetypeCatalog
{
    private readonly Dictionary<string, ArchetypeDto> _byId;

    public IReadOnlyList<ArchetypeDto> AllArchetypes { get; }

    private ArchetypeCatalog(IReadOnlyList<ArchetypeDto> archetypes)
    {
        AllArchetypes = archetypes;
        _byId = archetypes.ToDictionary(a => a.Id, StringComparer.Ordinal);
    }

    /// <summary>Returns the archetype with the given id, or null if not found.</summary>
    public ArchetypeDto? TryGet(string id)
        => _byId.TryGetValue(id, out var a) ? a : null;

    /// <summary>
    /// Loads the catalog from <paramref name="path"/>.
    /// Validates against <c>archetypes.schema.json</c>. Throws on failure.
    /// </summary>
    public static ArchetypeCatalog LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"Archetype catalog not found: {path}");

        var json = File.ReadAllText(path);

        var validation = SchemaValidator.Validate(json, Schema.Archetypes);
        if (!validation.IsValid)
            throw new InvalidOperationException(
                "archetypes.json failed schema validation: " +
                string.Join("; ", validation.Errors));

        var dto = JsonSerializer.Deserialize<ArchetypeCatalogDto>(json, JsonOptions.Wire)
            ?? throw new InvalidOperationException("archetypes.json deserialised to null.");

        return new ArchetypeCatalog(dto.Archetypes);
    }

    /// <summary>
    /// Searches upward from the current directory for
    /// <c>docs/c2-content/archetypes/archetypes.json</c> and loads it.
    /// Returns null if the file cannot be located (simulation continues without cast generation).
    /// </summary>
    public static ArchetypeCatalog? LoadDefault()
    {
        var path = FindDefault();
        if (path is null) return null;
        return LoadFromFile(path);
    }

    /// <summary>Walks up from CWD looking for the standard archetype catalog path.</summary>
    public static string? FindDefault()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 8; i++)
        {
            if (dir is null) break;
            var candidate = Path.Combine(
                dir.FullName, "docs", "c2-content", "archetypes", "archetypes.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    // ── Inhibition class parsing ──────────────────────────────────────────────

    public static InhibitionClass ParseInhibitionClass(string s) => s switch
    {
        "infidelity"             => InhibitionClass.Infidelity,
        "confrontation"          => InhibitionClass.Confrontation,
        "bodyImageEating"        => InhibitionClass.BodyImageEating,
        "publicEmotion"          => InhibitionClass.PublicEmotion,
        "physicalIntimacy"       => InhibitionClass.PhysicalIntimacy,
        "interpersonalConflict"  => InhibitionClass.InterpersonalConflict,
        "riskTaking"             => InhibitionClass.RiskTaking,
        "vulnerability"          => InhibitionClass.Vulnerability,
        _                        => InhibitionClass.Vulnerability
    };

    public static InhibitionAwareness ParseAwareness(string s) => s switch
    {
        "known"  => InhibitionAwareness.Known,
        "hidden" => InhibitionAwareness.Hidden,
        _        => InhibitionAwareness.Hidden
    };
}
