using Warden.Contracts.SchemaValidation;

namespace Warden.Orchestrator.Persistence;

/// <summary>
/// Scans a partial run root and identifies which Sonnet workers still need to be
/// dispatched (those missing a valid <c>result.json</c>).
/// </summary>
/// <remarks>
/// A worker is "done" iff its <c>result.json</c> exists AND passes schema validation.
/// Missing or schema-invalid result files cause a redispatch from scratch; the
/// worker's partial <c>prompt.txt</c> and <c>response.raw.json</c> are overwritten.
/// </remarks>
public sealed class ResumeScanner
{
    private readonly string _runsRoot;

    public ResumeScanner(string runsRoot)
    {
        _runsRoot = runsRoot;
    }

    /// <summary>
    /// Represents the persisted state of one Sonnet worker inside the run root.
    /// </summary>
    public sealed record WorkerState(
        string  WorkerId,
        string  SpecJson,
        bool    IsComplete,
        string? ResultJson);

    /// <summary>
    /// Enumerates all <c>sonnet-*/</c> sub-directories in the run root.
    /// For each, reports whether the worker is complete (schema-valid
    /// <c>result.json</c> present) or needs redispatch.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when the run root directory does not exist.
    /// </exception>
    public async Task<IReadOnlyList<WorkerState>> ScanSonnetWorkersAsync(
        string runId, CancellationToken ct = default)
    {
        var runRoot = RunLayout.RunRoot(_runsRoot, runId);
        if (!Directory.Exists(runRoot))
            throw new DirectoryNotFoundException($"Run root not found: '{runRoot}'.");

        var workers = new List<WorkerState>();

        var sonnetDirs = Directory
            .GetDirectories(runRoot, "sonnet-*")
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToArray();

        foreach (var dir in sonnetDirs)
        {
            ct.ThrowIfCancellationRequested();

            var workerId   = Path.GetFileName(dir);
            var specPath   = Path.Combine(dir, "spec.json");
            var resultPath = Path.Combine(dir, "result.json");

            if (!File.Exists(specPath))
                continue;

            var specJson = await File.ReadAllTextAsync(specPath, ct).ConfigureAwait(false);

            if (!File.Exists(resultPath))
            {
                workers.Add(new WorkerState(workerId, specJson, IsComplete: false, ResultJson: null));
                continue;
            }

            var resultJson = await File.ReadAllTextAsync(resultPath, ct).ConfigureAwait(false);
            var validation = SchemaValidator.Validate(resultJson, Schema.SonnetResult);

            if (!validation.IsValid)
            {
                workers.Add(new WorkerState(workerId, specJson, IsComplete: false, ResultJson: null));
                continue;
            }

            workers.Add(new WorkerState(workerId, specJson, IsComplete: true, ResultJson: resultJson));
        }

        return workers.AsReadOnly();
    }
}
