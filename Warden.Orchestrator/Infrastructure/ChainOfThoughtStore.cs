using System.Text.Json;
using Warden.Contracts;
using Warden.Contracts.Handshake;

namespace Warden.Orchestrator.Infrastructure;

/// <summary>
/// Writes chain-of-thought artefacts to the run directory tree described in SRD §2 Pillar B.
///
/// Directory layout under <c>&lt;runsRoot&gt;/&lt;runId&gt;/</c>:
/// <code>
/// batch-id.txt
/// haiku-01/
///   scenario.json
///   result.json
/// haiku-02/
///   ...
/// </code>
/// </summary>
public sealed class ChainOfThoughtStore
{
    private readonly string _runsRoot;

    public ChainOfThoughtStore(string runsRoot)
    {
        _runsRoot = runsRoot;
    }

    /// <summary>Persists the batch ID for a run (step 4 of <c>RunAsync</c>).</summary>
    public async Task WriteBatchIdAsync(string runId, string batchId, CancellationToken ct = default)
    {
        var dir = RunDir(runId);
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "batch-id.txt"), batchId, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Writes <c>scenario.json</c> for one Haiku worker directory.</summary>
    public async Task WriteScenarioAsync(
        string runId, string haikuId, ScenarioDto scenario, CancellationToken ct = default)
    {
        var dir = HaikuDir(runId, haikuId);
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(scenario, JsonOptions.Pretty);
        await File.WriteAllTextAsync(Path.Combine(dir, "scenario.json"), json, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Writes <c>result.json</c> for one Haiku worker directory.</summary>
    public async Task WriteResultAsync(
        string runId, string haikuId, HaikuResult result, CancellationToken ct = default)
    {
        var dir = HaikuDir(runId, haikuId);
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(result, JsonOptions.Pretty);
        await File.WriteAllTextAsync(Path.Combine(dir, "result.json"), json, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Returns the absolute path of a Haiku worker directory (for test verification).</summary>
    public string HaikuDir(string runId, string haikuId)
        => Path.Combine(_runsRoot, runId, haikuId);

    private string RunDir(string runId) => Path.Combine(_runsRoot, runId);
}
