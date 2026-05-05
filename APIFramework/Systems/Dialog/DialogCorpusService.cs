using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Warden.Contracts.SchemaValidation;

namespace APIFramework.Systems.Dialog;

/// <summary>
/// Immutable, schema-validated phrase corpus loaded at simulation boot.
/// Exposes O(1) queries by (register, context) pair.
/// </summary>
public sealed class DialogCorpusService
{
    private readonly List<PhraseFragment> _all;
    private readonly Dictionary<(string Register, string Context), List<PhraseFragment>> _index;

    /// <param name="corpusJson">Raw JSON text of the corpus file.</param>
    /// <exception cref="InvalidOperationException">Schema validation failed.</exception>
    public DialogCorpusService(string corpusJson)
    {
        var result = SchemaValidator.Validate(corpusJson, Schema.Corpus);
        if (!result.IsValid)
            throw new InvalidOperationException(
                $"Corpus validation failed: {string.Join("; ", result.Errors)}");

        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        };

        var dto = JsonSerializer.Deserialize<CorpusDto>(corpusJson, opts)
            ?? throw new InvalidOperationException("Corpus deserialization returned null.");

        _all   = new List<PhraseFragment>(dto.Fragments);
        _index = new Dictionary<(string, string), List<PhraseFragment>>();

        foreach (var f in _all)
        {
            var key = (f.Register, f.Context);
            if (!_index.TryGetValue(key, out var bucket))
                _index[key] = bucket = new List<PhraseFragment>();
            bucket.Add(f);
        }
    }

    /// <summary>All fragments in the corpus.</summary>
    public IReadOnlyList<PhraseFragment> AllFragments => _all;

    /// <summary>Returns all fragments matching the given register and context.</summary>
    /// <param name="register">Vocabulary register (e.g. "casual", "formal").</param>
    /// <param name="context">Dialog context (e.g. "greeting", "lashOut").</param>
    /// <returns>Matching fragments, or an empty array when the bucket has no entries.</returns>
    public IReadOnlyList<PhraseFragment> QueryByRegisterAndContext(string register, string context)
        => _index.TryGetValue((register, context), out var list)
            ? list
            : Array.Empty<PhraseFragment>();

    // -- Static factory helpers ------------------------------------------------

    /// <summary>Loads the corpus from a file path.</summary>
    /// <param name="path">Absolute or CWD-relative path to the corpus JSON file.</param>
    /// <returns>A new corpus service populated from the file.</returns>
    public static DialogCorpusService LoadFromFile(string path)
        => new(File.ReadAllText(path));

    /// <summary>
    /// Walks up from CWD (max 6 levels) and returns the first directory that
    /// contains <paramref name="relativePath"/>.  Returns null if not found.
    /// </summary>
    /// <param name="relativePath">Relative path to search for at each ancestor directory.</param>
    /// <returns>The full path to the first match, or null if none was found within 6 levels.</returns>
    public static string? FindCorpusFile(string relativePath)
    {
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        return null;
    }

    // -- DTOs (internal — only used for deserialization) -----------------------

    private sealed class CorpusDto
    {
        public string          SchemaVersion { get; set; } = string.Empty;
        public PhraseFragment[] Fragments     { get; set; } = Array.Empty<PhraseFragment>();
    }
}

// -- Public data model ---------------------------------------------------------

/// <summary>
/// A single hand-authored phrase fragment from the corpus.
/// Instances are immutable after deserialization.
/// </summary>
public sealed class PhraseFragment
{
    /// <summary>Stable identifier; used for tie-break ordering and per-listener history keys.</summary>
    public string Id             { get; set; } = string.Empty;
    /// <summary>The phrase text spoken by an NPC.</summary>
    public string Text           { get; set; } = string.Empty;
    /// <summary>Vocabulary register matched against the speaker's <c>PersonalityComponent</c>.</summary>
    public string Register       { get; set; } = string.Empty;
    /// <summary>Dialog context (e.g. "greeting", "complaint") matched against the speaker's intent or drives.</summary>
    public string Context        { get; set; } = string.Empty;

    /// <summary>Drive name → ordinal level ("low" / "mid" / "high") expected at speak time.</summary>
    public Dictionary<string, string> ValenceProfile { get; set; } = new();

    /// <summary>Optional list of relationship-fit tags; null when not constrained.</summary>
    public string[]? RelationshipFit { get; set; }

    /// <summary>Score weight that promotes more-distinctive fragments toward narrative recording.</summary>
    public int Noteworthiness { get; set; }
}
