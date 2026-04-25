using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warden.Orchestrator.Cache;

/// <summary>
/// Loads the static corpus declared in <c>cached-corpus.manifest.json</c> and assembles
/// the two cached slabs that every <see cref="PromptCacheManager"/> call uses:
/// <list type="bullet">
///   <item>Slab 1 — hard-coded role frame (constant; ends with the canonical boundary marker).</item>
///   <item>Slab 2 — corpus assembled from the manifest's file entries in declaration order.</item>
/// </list>
/// The assembled corpus is cached in memory and invalidated when any corpus file's mtime
/// or the manifest's mtime changes.
/// </summary>
public sealed class CachedPrefixSource
{
    /// <summary>
    /// Hard-coded role frame for slab 1.  Begins with a labelled header and ends with
    /// the canonical <c>\n\n---\n\n</c> boundary marker (AT-07).
    /// </summary>
    public static readonly string RoleFrameText =
        "# Warden Orchestration Agent — Role Frame\n\n" +
        "You are a Warden orchestration agent operating in the 1-5-25 Claude Army workflow.\n" +
        "Your role: execute engineering Work Packets against the ECS Simulation Engine.\n\n" +
        "Rules:\n" +
        "- Fail closed on any error; emit outcome=\"blocked\" and stop. Do not retry.\n" +
        "- Never recurse or spawn helper agents of your own.\n" +
        "- Never invoke api.anthropic.com at runtime; the Anthropic API is design-time only.\n" +
        "- Stay in your assigned worktree; do not touch other branches.\n" +
        "- Respect the packet's Non-goals; do exactly what is asked, no more.\n" +
        "\n---\n\n";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _manifestPath;
    private readonly string _baseDirectory;
    private readonly object _lock = new();

    private string?                          _cachedCorpus;
    private DateTime                         _manifestMtime;
    private List<(string Path, DateTime Mtime)> _fileState = [];

    /// <param name="manifestPath">Absolute path to <c>cached-corpus.manifest.json</c>.</param>
    /// <param name="baseDirectory">
    /// Root against which the manifest's relative <c>path</c> entries are resolved.
    /// Typically the repo root in production; a temp directory in tests.
    /// </param>
    /// <exception cref="FileNotFoundException">
    /// Thrown at construction time (AT-09) if the manifest or any listed corpus file is missing.
    /// </exception>
    public CachedPrefixSource(string manifestPath, string baseDirectory)
    {
        _manifestPath  = Path.GetFullPath(manifestPath);
        _baseDirectory = Path.GetFullPath(baseDirectory);

        if (!File.Exists(_manifestPath))
            throw new FileNotFoundException("Corpus manifest not found.", _manifestPath);

        // Fail-fast: validate all paths before the first call can observe missing files.
        var paths = ResolveAndValidatePaths(_manifestPath, _baseDirectory);
        _manifestMtime = File.GetLastWriteTimeUtc(_manifestPath);
        _fileState = paths
            .Select(p => (p, File.GetLastWriteTimeUtc(p)))
            .ToList();
    }

    /// <summary>Returns the constant role frame text (slab 1).</summary>
    public string GetRoleFrameText() => RoleFrameText;

    /// <summary>
    /// Returns the assembled corpus text (slab 2).
    /// Checks file and manifest mtimes on every call; reloads from disk when any changed.
    /// </summary>
    public string GetCorpusText()
    {
        lock (_lock)
        {
            if (NeedsReload())
                Reload();
            return _cachedCorpus!;
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private bool NeedsReload()
    {
        if (_cachedCorpus is null)
            return true;
        if (File.GetLastWriteTimeUtc(_manifestPath) != _manifestMtime)
            return true;
        foreach (var (path, mtime) in _fileState)
            if (File.GetLastWriteTimeUtc(path) != mtime)
                return true;
        return false;
    }

    private void Reload()
    {
        var paths = ResolveAndValidatePaths(_manifestPath, _baseDirectory);

        var sb = new StringBuilder();
        var newState = new List<(string, DateTime)>(paths.Count);

        for (int i = 0; i < paths.Count; i++)
        {
            if (i > 0) sb.Append("\n\n---\n\n");
            sb.Append(File.ReadAllText(paths[i]));
            newState.Add((paths[i], File.GetLastWriteTimeUtc(paths[i])));
        }

        _cachedCorpus  = sb.ToString();
        _fileState     = newState;
        _manifestMtime = File.GetLastWriteTimeUtc(_manifestPath);
    }

    private static List<string> ResolveAndValidatePaths(string manifestPath, string baseDirectory)
    {
        var json = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<CorpusManifest>(json, JsonOptions)
            ?? throw new InvalidDataException($"Failed to parse corpus manifest: {manifestPath}");

        var resolved = new List<string>(manifest.Entries.Length);
        foreach (var entry in manifest.Entries)
        {
            var fullPath = Path.GetFullPath(Path.Combine(baseDirectory, entry.Path));
            if (!File.Exists(fullPath))
                throw new FileNotFoundException(
                    $"Corpus file '{entry.Path}' (purpose: {entry.Purpose}) not found at resolved path.",
                    fullPath);
            resolved.Add(fullPath);
        }
        return resolved;
    }

    // ── Manifest DTOs ──────────────────────────────────────────────────────────

    private sealed record CorpusManifest(string Version, CorpusEntry[] Entries);

    private sealed record CorpusEntry(
        string Path,
        int    Slab,
        string Purpose);
}
