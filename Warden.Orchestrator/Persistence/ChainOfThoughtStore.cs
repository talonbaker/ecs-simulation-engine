using System.Text;
using Warden.Orchestrator.Dispatcher;

namespace Warden.Orchestrator.Persistence;

/// <summary>
/// Concrete <see cref="IChainOfThoughtStore"/> that persists Sonnet chain-of-thought
/// artefacts to the <c>./runs/&lt;runId&gt;/</c> directory tree described in SRD §2 Pillar B.
/// </summary>
/// <remarks>
/// Write-order invariant per worker: spec.json → prompt.txt → response.raw.json → result.json.
/// The orchestrator considers a worker "done" iff <c>result.json</c> exists; any worker
/// missing it is redispatched from scratch on resume.
/// </remarks>
public sealed class ChainOfThoughtStore : IChainOfThoughtStore
{
    private readonly string         _runsRoot;
    private readonly SemaphoreSlim  _eventsGate = new(1, 1);

    public ChainOfThoughtStore(string runsRoot)
    {
        _runsRoot = runsRoot;
    }

    // -- Run lifecycle ------------------------------------------------------------

    /// <summary>
    /// Creates the run root directory. Throws <see cref="InvalidOperationException"/>
    /// if the directory already exists (run-ID collision guard, SRD AT-06).
    /// </summary>
    public void InitRun(string runId)
    {
        var runRoot = RunLayout.RunRoot(_runsRoot, runId);
        if (Directory.Exists(runRoot))
            throw new InvalidOperationException(
                $"Run-ID collision: '{runId}' already exists at '{runRoot}'. " +
                "Choose a different --run-id or delete the existing run directory.");
        Directory.CreateDirectory(runRoot);
    }

    /// <summary>Writes <c>mission.md</c> to the run root.</summary>
    public Task PersistMissionAsync(string runId, string missionText, CancellationToken ct = default)
        => WriteFileAsync(RunLayout.MissionFile(_runsRoot, runId), missionText, ct);

    /// <summary>
    /// Appends one event line to <c>events.jsonl</c>.
    /// The file is strictly append-only; no line is ever rewritten.
    /// Serialisation is serialised via a semaphore so concurrent callers
    /// never interleave partial lines.
    /// </summary>
    public async Task AppendEventAsync(string runId, string eventJson, CancellationToken ct = default)
    {
        var path = RunLayout.EventsFile(_runsRoot, runId);
        EnsureDir(path);

        await _eventsGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(path, eventJson + "\n", Encoding.UTF8, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            _eventsGate.Release();
        }
    }

    // -- IChainOfThoughtStore (Sonnet worker artefacts) ---------------------------

    /// <inheritdoc/>
    public Task PersistSpecAsync(
        string runId, string sonnetId, string specJson, CancellationToken ct = default)
        => WriteFileAsync(RunLayout.SpecFile(_runsRoot, runId, sonnetId), specJson, ct);

    /// <inheritdoc/>
    public Task PersistPromptAsync(
        string runId, string sonnetId, string promptText, CancellationToken ct = default)
        => WriteFileAsync(RunLayout.PromptFile(_runsRoot, runId, sonnetId), promptText, ct);

    /// <inheritdoc/>
    public Task PersistRawResponseAsync(
        string runId, string sonnetId, string responseJson, CancellationToken ct = default)
        => WriteFileAsync(RunLayout.RawResponseFile(_runsRoot, runId, sonnetId), responseJson, ct);

    /// <inheritdoc/>
    public Task PersistResultAsync(
        string runId, string sonnetId, string resultJson, CancellationToken ct = default)
        => WriteFileAsync(RunLayout.ResultFile(_runsRoot, runId, sonnetId), resultJson, ct);

    // -- Helpers ------------------------------------------------------------------

    private static async Task WriteFileAsync(string path, string content, CancellationToken ct)
    {
        EnsureDir(path);
        await File.WriteAllTextAsync(path, content, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static void EnsureDir(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }
}
