using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Warden.Contracts;
using Warden.Contracts.Handshake;

namespace Warden.Orchestrator.Batch;

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
    /// </summary>
    /// <returns>
    /// <c>Unique</c>: first-seen scenario for each distinct hash.<br/>
    /// <c>DupToOrig</c>: maps each suppressed scenario ID to the original scenario ID it matches.
    /// </returns>
    public (List<ScenarioDto> Unique, Dictionary<string, string> DupToOrig) Deduplicate(
        IReadOnlyList<ScenarioDto> scenarios)
    {
        var unique     = new List<ScenarioDto>();
        var seenHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var dupToOrig  = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var s in scenarios)
        {
            var hash = ComputeHash(s);
            if (seenHashes.TryGetValue(hash, out var origId))
            {
                dupToOrig[s.ScenarioId] = origId;
                _log.LogInformation(
                    "Scenario {DupId} is a duplicate of {OrigId} (hash prefix {Hash}); " +
                    "suppressed — cost saving.",
                    s.ScenarioId, origId, hash[..8]);
            }
            else
            {
                seenHashes[hash] = s.ScenarioId;
                unique.Add(s);
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
