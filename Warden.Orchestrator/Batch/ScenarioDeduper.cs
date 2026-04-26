using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Warden.Contracts;
using Warden.Contracts.Handshake;

namespace Warden.Orchestrator.Batch;

/// <summary>
/// Composite key uniquely identifying one scenario across multiple ScenarioBatches in the same run.
/// The same ScenarioId may appear in different batches; this key prevents collisions in all lookup
/// dictionaries and in the Anthropic <c>custom_id</c> field.
/// </summary>
internal readonly record struct ScenarioKey(string BatchId, string ScenarioId);

/// <summary>
/// Hashes each scenario by <c>(seed, configDelta, commands, assertions)</c> and
/// collapses duplicates before batch submission. A dedupe hit is logged as a cost saving.
/// </summary>
internal sealed class ScenarioDeduper
{
    private readonly ILogger _log;

    public ScenarioDeduper(ILogger log) => _log = log;

    /// <summary>
    /// Partitions <paramref name="scenarios"/> into unique entries and a duplicate map.
    /// Two scenarios from different batches with the same content hash are still deduplicated
    /// (cost saving); their distinct <see cref="ScenarioKey"/>s are preserved in the dup map
    /// so the result-reattachment loop can stamp the correct <c>ParentBatchId</c> on each.
    /// </summary>
    /// <returns>
    /// <c>Unique</c>: first-seen <c>(batchId, scenario)</c> pair for each distinct content hash.<br/>
    /// <c>DupToOrig</c>: maps each suppressed <see cref="ScenarioKey"/> to the original key it matches.
    /// </returns>
    public (List<(string BatchId, ScenarioDto Scenario)> Unique, Dictionary<ScenarioKey, ScenarioKey> DupToOrig) Deduplicate(
        IReadOnlyList<(string BatchId, ScenarioDto Scenario)> scenarios)
    {
        var unique     = new List<(string BatchId, ScenarioDto Scenario)>();
        var seenHashes = new Dictionary<string, ScenarioKey>(StringComparer.Ordinal);
        var dupToOrig  = new Dictionary<ScenarioKey, ScenarioKey>();

        foreach (var (batchId, s) in scenarios)
        {
            var key  = new ScenarioKey(batchId, s.ScenarioId);
            var hash = ComputeHash(s);
            if (seenHashes.TryGetValue(hash, out var origKey))
            {
                dupToOrig[key] = origKey;
                _log.LogInformation(
                    "Scenario {DupBatchId}::{DupId} is a duplicate of {OrigBatchId}::{OrigId} " +
                    "(hash prefix {Hash}); suppressed — cost saving.",
                    batchId, s.ScenarioId, origKey.BatchId, origKey.ScenarioId, hash[..8]);
            }
            else
            {
                seenHashes[hash] = key;
                unique.Add((batchId, s));
            }
        }

        return (unique, dupToOrig);
    }

    internal static string ComputeHash(ScenarioDto scenario)
    {
        var key = new
        {
            scenario.Seed,
            scenario.ConfigDelta,
            scenario.Commands,
            Assertions = scenario.Assertions
        };

        var json  = JsonSerializer.Serialize(key, JsonOptions.Wire);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}
