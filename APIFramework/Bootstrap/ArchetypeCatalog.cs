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

// -- DTOs (deserialization targets) --------------------------------------------

/// <summary>Top-level deserialization target for <c>archetypes.json</c>.</summary>
public sealed class ArchetypeCatalogDto
{
    /// <summary>Schema version string declared by the JSON file.</summary>
    public string         SchemaVersion { get; set; } = "";
    /// <summary>All archetype definitions.</summary>
    public ArchetypeDto[] Archetypes    { get; set; } = Array.Empty<ArchetypeDto>();
}

/// <summary>One archetype entry: the full data needed to spawn an NPC of this type.</summary>
public sealed class ArchetypeDto
{
    /// <summary>Stable archetype identifier referenced by NPC slots and relationship hints.</summary>
    public string   Id          { get; set; } = "";
    /// <summary>Human-readable display name (e.g. "The Warden", "The Crush").</summary>
    public string   DisplayName { get; set; } = "";

    /// <summary>Lower-case drive names (e.g. "irritation") whose baseline is sampled from the elevated range.</summary>
    [JsonPropertyName("chronicallyElevatedDrives")]
    public string[] ElevatedDrives  { get; set; } = Array.Empty<string>();

    /// <summary>Lower-case drive names whose baseline is sampled from the depressed range.</summary>
    [JsonPropertyName("chronicallyDepressedDrives")]
    public string[] DepressedDrives { get; set; } = Array.Empty<string>();

    /// <summary>Big-Five personality ranges sampled per axis at spawn time.</summary>
    public PersonalityRangesDto   PersonalityRanges    { get; set; } = new();
    /// <summary>Vocabulary register tag — either a single string or an array of strings (parsed by <see cref="GetRegisters"/>).</summary>
    public JsonElement            VocabularyRegister   { get; set; }   // string or string[]
    /// <summary>Inclusive [min, max] willpower baseline range; the same value is also used as starting Current.</summary>
    public int[]                  WillpowerBaselineRange { get; set; } = new[] { 40, 60 };
    /// <summary>Pool of "deal" strings (the archetype's deep narrative bargain); one is sampled per spawn.</summary>
    public string[]               DealOptions          { get; set; } = Array.Empty<string>();
    /// <summary>Silhouette appearance pools; one option is sampled from each non-empty list.</summary>
    public SilhouetteFamilyDto    SilhouetteFamily     { get; set; } = new();
    /// <summary>Starter inhibitions seeded onto the NPC's <c>InhibitionsComponent</c>.</summary>
    public StarterInhibitionDto[] StarterInhibitions   { get; set; } = Array.Empty<StarterInhibitionDto>();
    /// <summary>Optional hints used by <see cref="CastGenerator.SeedRelationships"/> to seed signature relationships.</summary>
    public RelationshipSpawnHintsDto? RelationshipSpawnHints { get; set; }
    /// <summary>Optional dialog calcification hints (priority tags for the dialog system).</summary>
    public DialogHintsDto?        DialogHints          { get; set; }

    /// <summary>Returns the vocabulary register(s) as a string array. Single strings become a one-element array.</summary>
    /// <returns>The parsed register list; defaults to a single "casual" entry if absent or unrecognised.</returns>
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

/// <summary>Big-Five personality sampling ranges for an archetype. Each entry is an inclusive [min, max] pair.</summary>
public sealed class PersonalityRangesDto
{
    /// <summary>Openness range as [min, max] integers (typically -2..+2).</summary>
    public int[]? Openness          { get; set; }
    /// <summary>Conscientiousness range as [min, max] integers (typically -2..+2).</summary>
    public int[]? Conscientiousness { get; set; }
    /// <summary>Extraversion range as [min, max] integers (typically -2..+2).</summary>
    public int[]? Extraversion      { get; set; }
    /// <summary>Agreeableness range as [min, max] integers (typically -2..+2).</summary>
    public int[]? Agreeableness     { get; set; }
    /// <summary>Neuroticism range as [min, max] integers (typically -2..+2).</summary>
    public int[]? Neuroticism       { get; set; }
}

/// <summary>Pools of silhouette/appearance descriptors; the cast generator samples one item from each.</summary>
public sealed class SilhouetteFamilyDto
{
    /// <summary>Candidate height descriptors (e.g. "tall", "average").</summary>
    public string[] Heights          { get; set; } = Array.Empty<string>();
    /// <summary>Candidate build descriptors (e.g. "lean", "stocky").</summary>
    public string[] Builds           { get; set; } = Array.Empty<string>();
    /// <summary>Candidate hair descriptors.</summary>
    public string[] Hair             { get; set; } = Array.Empty<string>();
    /// <summary>Candidate headwear descriptors.</summary>
    public string[] Headwear         { get; set; } = Array.Empty<string>();
    /// <summary>Candidate dominant-color descriptors.</summary>
    public string[] DominantColors   { get; set; } = Array.Empty<string>();
    /// <summary>Candidate distinctive-item descriptors (lanyard, watch, etc.).</summary>
    public string[] DistinctiveItems { get; set; } = Array.Empty<string>();
}

/// <summary>One starter inhibition spec, sampled into a single <c>Inhibition</c> at spawn.</summary>
public sealed class StarterInhibitionDto
{
    /// <summary>Inhibition class enum string (e.g. "infidelity", "publicEmotion"); parsed by <see cref="ArchetypeCatalog.ParseInhibitionClass"/>.</summary>
    [JsonPropertyName("class")]
    public string Class         { get; set; } = "";
    /// <summary>Inclusive [min, max] strength range (0–100).</summary>
    public int[]  StrengthRange { get; set; } = new[] { 0, 100 };
    /// <summary>Awareness enum string ("known" or "hidden"); parsed by <see cref="ArchetypeCatalog.ParseAwareness"/>.</summary>
    public string Awareness     { get; set; } = "hidden";
}

/// <summary>Relationship spawn hints — used by <see cref="CastGenerator.SeedRelationships"/> to wire a signature relationship for this archetype.</summary>
public sealed class RelationshipSpawnHintsDto
{
    /// <summary>Relationship pattern enum string (e.g. "activeAffair", "secretCrush"); parsed by the cast generator.</summary>
    public string   Pattern                      { get; set; } = "";
    /// <summary>Preferred target archetype ids; if any match the cast, one is chosen at random.</summary>
    public string[] TargetArchetypePreferences   { get; set; } = Array.Empty<string>();
    /// <summary>When true this NPC is the target (B in "A → B"); when false it is the initiator (A).</summary>
    public bool     IsTarget                     { get; set; }
}

/// <summary>Optional per-archetype dialog hints.</summary>
public sealed class DialogHintsDto
{
    /// <summary>Tag names that the dialog system should prioritise when considering calcification for this archetype.</summary>
    public string[] CalcifyPriorityTags { get; set; } = Array.Empty<string>();
}

// -- ArchetypeCatalog -----------------------------------------------------------

/// <summary>
/// Loads, validates, and exposes the archetype catalog from
/// <c>docs/c2-content/archetypes/archetypes.json</c>.
/// Fail-closed: any validation or parse error throws <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class ArchetypeCatalog
{
    private readonly Dictionary<string, ArchetypeDto> _byId;

    /// <summary>All archetype definitions, in JSON declaration order.</summary>
    public IReadOnlyList<ArchetypeDto> AllArchetypes { get; }

    private ArchetypeCatalog(IReadOnlyList<ArchetypeDto> archetypes)
    {
        AllArchetypes = archetypes;
        _byId = archetypes.ToDictionary(a => a.Id, StringComparer.Ordinal);
    }

    /// <summary>Returns the archetype with the given id, or null if not found.</summary>
    /// <param name="id">Archetype identifier as it appears in <c>archetypes.json</c>.</param>
    /// <returns>The matching <see cref="ArchetypeDto"/>, or <c>null</c> if no archetype has that id.</returns>
    public ArchetypeDto? TryGet(string id)
        => _byId.TryGetValue(id, out var a) ? a : null;

    /// <summary>
    /// Loads the catalog from <paramref name="path"/>.
    /// Validates against <c>archetypes.schema.json</c>. Throws on failure.
    /// </summary>
    /// <param name="path">Absolute or relative path to the archetypes JSON file.</param>
    /// <returns>A populated <see cref="ArchetypeCatalog"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the file is missing, fails schema validation, or deserialises to null.</exception>
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
    /// <returns>The loaded catalog, or <c>null</c> when the default file cannot be discovered.</returns>
    public static ArchetypeCatalog? LoadDefault()
    {
        var path = FindDefault();
        if (path is null) return null;
        return LoadFromFile(path);
    }

    /// <summary>Walks up from CWD looking for the standard archetype catalog path.</summary>
    /// <returns>The discovered absolute path, or <c>null</c> if not found within 8 directory levels.</returns>
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

    // -- Inhibition class parsing ----------------------------------------------

    /// <summary>Maps an archetype JSON inhibition-class string to an <see cref="InhibitionClass"/>; unknown values fall back to <see cref="InhibitionClass.Vulnerability"/>.</summary>
    /// <param name="s">Inhibition class string from the JSON (e.g. "infidelity", "publicEmotion").</param>
    /// <returns>The corresponding <see cref="InhibitionClass"/> enum value.</returns>
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

    /// <summary>Maps an archetype JSON awareness string ("known", "hidden") to an <see cref="InhibitionAwareness"/>; unknown values fall back to <see cref="InhibitionAwareness.Hidden"/>.</summary>
    /// <param name="s">Awareness string from the JSON.</param>
    /// <returns>The corresponding <see cref="InhibitionAwareness"/> enum value.</returns>
    public static InhibitionAwareness ParseAwareness(string s) => s switch
    {
        "known"  => InhibitionAwareness.Known,
        "hidden" => InhibitionAwareness.Hidden,
        _        => InhibitionAwareness.Hidden
    };
}
