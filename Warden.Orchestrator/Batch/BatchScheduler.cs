using System.Text.Json;
using Microsoft.Extensions.Logging;
using Warden.Anthropic;
using Warden.Contracts;
using Warden.Contracts.Handshake;
using Warden.Contracts.SchemaValidation;
using Warden.Orchestrator.Cache;
using Warden.Orchestrator.Infrastructure;
// Disambiguate: Warden.Contracts.Handshake.TokenUsage vs Warden.Anthropic.TokenUsage
using TokenUsage = Warden.Contracts.Handshake.TokenUsage;

namespace Warden.Orchestrator.Batch;

/// <summary>
/// Submits Haiku scenarios as a single Message Batches API job, polls for completion,
/// streams results back, and deduplicates identical scenarios within and across missions.
/// Owns the 50% Haiku batch discount described in SRD §2 Pillar D.2.
/// </summary>
public sealed class BatchScheduler
{
    private readonly IAnthropicClient         _client;
    private readonly PromptCacheManager      _cache;
    private readonly ChainOfThoughtStore     _cot;
    private readonly CostLedger              _ledger;
    private readonly ILogger<BatchScheduler> _log;
    private readonly BatchPoller             _poller;

    public BatchScheduler(
        IAnthropicClient        client,
        PromptCacheManager      cache,
        ChainOfThoughtStore     cot,
        CostLedger              ledger,
        ILogger<BatchScheduler> log)
        : this(client, cache, cot, ledger, log, new BatchPoller()) { }

    internal BatchScheduler(
        IAnthropicClient        client,
        PromptCacheManager      cache,
        ChainOfThoughtStore     cot,
        CostLedger              ledger,
        ILogger<BatchScheduler> log,
        BatchPoller             poller)
    {
        _client = client;
        _cache  = cache;
        _cot    = cot;
        _ledger = ledger;
        _log    = log;
        _poller = poller;
    }

    /// <summary>
    /// Runs the full batch lifecycle for the given scenario batches.
    /// </summary>
    /// <param name="runId">Identifies the run for chain-of-thought persistence.</param>
    /// <param name="batches">Up to 25 total scenarios across all batches.</param>
    /// <param name="ct">Cancellation propagated through the poll loop.</param>
    /// <returns>
    /// One <see cref="HaikuResult"/> per input scenario (including reattached duplicates),
    /// in input order.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when total scenarios exceed 25.</exception>
    public async Task<IReadOnlyList<HaikuResult>> RunAsync(
        string runId,
        IReadOnlyList<ScenarioBatch> batches,
        CancellationToken ct)
    {
        // Step 1: Flatten — hard limit of 25 enforced here.
        // Each entry carries its parent BatchId so scenarios from different batches
        // with the same ScenarioId can coexist without key collisions.
        var allScenarioPairs = batches
            .SelectMany(b => b.Scenarios.Select(s => (BatchId: b.BatchId, Scenario: s)))
            .ToList();

        if (allScenarioPairs.Count > 25)
            throw new InvalidOperationException(
                $"Total scenario count {allScenarioPairs.Count} exceeds the 25-scenario maximum. " +
                "The orchestrator must enforce this limit before calling RunAsync.");

        if (allScenarioPairs.Count == 0)
            return Array.Empty<HaikuResult>();

        // Pre-assign haiku-NN IDs to every input scenario in stable input order.
        // Keyed by composite (BatchId, ScenarioId) so two batches with overlapping
        // scenario IDs each get distinct haiku-NN assignments.
        var haikuIdFor = allScenarioPairs
            .Select((p, i) => (Key: new ScenarioKey(p.BatchId, p.Scenario.ScenarioId), HaikuId: $"haiku-{i + 1:D2}"))
            .ToDictionary(x => x.Key, x => x.HaikuId);

        // Step 2: Deduplicate by content hash.
        // uniquePairs: one entry per distinct scenario hash.
        // dupToOrig: ScenarioKey → ScenarioKey for every suppressed duplicate.
        var deduper = new ScenarioDeduper(_log);
        var (uniquePairs, dupToOrig) = deduper.Deduplicate(allScenarioPairs);

        // Step 3: Build one batch request (one entry per unique scenario).
        // custom_id is a composite "<batchId>::<scenarioId>" so Anthropic's result stream
        // can be parsed back to the correct ScenarioKey without any side-channel lookup.
        var entries = uniquePairs
            .Select(p => new BatchRequestEntry(
                BuildCompositeId(p.BatchId, p.Scenario.ScenarioId),
                _cache.BuildRequest(
                    ModelId.HaikuV45,
                    JsonSerializer.Serialize(p.Scenario, JsonOptions.Wire),
                    expectedTotalLatency: TimeSpan.FromMinutes(30))))
            .ToList();

        // Step 4: Submit
        var apiSubmission = await _client
            .CreateBatchAsync(new BatchRequest(entries), ct)
            .ConfigureAwait(false);

        _log.LogInformation(
            "Batch {BatchId} submitted for run {RunId}: {Unique} unique scenario(s), {Deduped} deduped.",
            apiSubmission.Id, runId, uniquePairs.Count, dupToOrig.Count);

        await _cot.WriteBatchIdAsync(runId, apiSubmission.Id, ct).ConfigureAwait(false);

        // Step 5: Poll until the batch ends
        await _poller.PollUntilEndedAsync(_client, apiSubmission.Id, ct).ConfigureAwait(false);

        // Step 6: Stream, validate, and collect results for unique scenarios
        var uniqueResults = new Dictionary<ScenarioKey, HaikuResult>();

        await foreach (var entry in _client
            .StreamBatchResultsAsync(apiSubmission.Id, ct)
            .ConfigureAwait(false))
        {
            var key     = ParseCompositeCustomId(entry.CustomId);
            var haikuId = haikuIdFor[key];

            var result = entry.Result is SucceededResult succeeded
                ? ParseSucceeded(succeeded, key.BatchId, haikuId, key.ScenarioId)
                : BlockedEntry(key.ScenarioId, key.BatchId, haikuId);

            uniqueResults[key] = result;
        }

        // Step 7: Write ledger (unique calls only), write CoT for all scenarios,
        //         reattach duplicates, and assemble the ordered output list
        var finalResults = new List<HaikuResult>(allScenarioPairs.Count);

        foreach (var (batchId, scenario) in allScenarioPairs)
        {
            var key     = new ScenarioKey(batchId, scenario.ScenarioId);
            var haikuId = haikuIdFor[key];
            var isDup   = dupToOrig.TryGetValue(key, out var origKey);

            HaikuResult result;

            if (!isDup)
            {
                result = uniqueResults.TryGetValue(key, out var r)
                    ? r
                    : BlockedEntry(scenario.ScenarioId, batchId, haikuId);

                // Ledger: one line per unique Haiku call (AT-07)
                await _ledger
                    .AppendAsync(BuildLedgerEntry(runId, haikuId, result), ct)
                    .ConfigureAwait(false);
            }
            else
            {
                // Duplicate: clone the original result, update per-scenario fields.
                // The duplicate may belong to a different parent ScenarioBatch than
                // the original (cross-Sonnet content dedup), so re-stamp ParentBatchId
                // from the duplicate scenario's own parent.
                var origResult = uniqueResults.TryGetValue(origKey, out var o)
                    ? o
                    : BlockedEntry(origKey.ScenarioId, origKey.BatchId, haikuIdFor[origKey]);

                result = origResult with
                {
                    ScenarioId    = scenario.ScenarioId,
                    WorkerId      = haikuId,
                    ParentBatchId = batchId
                };
            }

            // CoT: write for every scenario, unique and duplicate alike (AT-08)
            await _cot.WriteScenarioAsync(runId, haikuId, scenario, ct).ConfigureAwait(false);
            await _cot.WriteResultAsync(runId, haikuId, result, ct).ConfigureAwait(false);

            finalResults.Add(result);
        }

        _log.LogInformation(
            "Run {RunId} complete: {Total} result(s) ({Unique} unique, {Dup} reattached duplicates).",
            runId, finalResults.Count, uniqueResults.Count, dupToOrig.Count);

        return finalResults.AsReadOnly();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Encodes a composite Anthropic <c>custom_id</c> from the ScenarioBatch id and scenario id.
    /// Validates against Anthropic's 64-character limit at construction time.
    /// </summary>
    private static string BuildCompositeId(string batchId, string scenarioId)
    {
        var composite = $"{batchId}::{scenarioId}";
        if (composite.Length > 64)
            throw new InvalidOperationException(
                $"Composite custom_id '{composite}' exceeds Anthropic's 64-char limit. " +
                $"Shorten the scenarioBatch.batchId in the upstream Sonnet result.");
        return composite;
    }

    /// <summary>
    /// Parses a composite <c>custom_id</c> back to its <see cref="ScenarioKey"/> components.
    /// Uses <c>LastIndexOf</c> so a batchId containing <c>::</c> is handled correctly.
    /// </summary>
    private static ScenarioKey ParseCompositeCustomId(string customId)
    {
        var idx = customId.LastIndexOf("::", StringComparison.Ordinal);
        if (idx < 0)
            throw new InvalidOperationException(
                $"Malformed custom_id '{customId}' — expected '<batchId>::<scenarioId>'.");
        return new ScenarioKey(customId[..idx], customId[(idx + 2)..]);
    }

    private HaikuResult ParseSucceeded(
        SucceededResult succeeded,
        string batchId,
        string haikuId,
        string scenarioId)
    {
        var text = succeeded.Message.Content
            .OfType<TextBlock>()
            .FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            _log.LogWarning(
                "Scenario {ScenarioId}: batch result has no text block — treating as blocked.",
                scenarioId);
            return BlockedEntry(scenarioId, batchId, haikuId);
        }

        // Defensive: strip prose/markdown wrapping. The role frame instructs the model
        // to return raw JSON; this extracts the JSON object between the first '{' and
        // the last '}' to handle any preamble or code-fence drift.
        var jsonStart = text.IndexOf('{');
        var jsonEnd   = text.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
            text = text.Substring(jsonStart, jsonEnd - jsonStart + 1);

        HaikuResult parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<HaikuResult>(text, JsonOptions.Wire)
                ?? throw new JsonException("Deserialized HaikuResult was null.");
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex,
                "Scenario {ScenarioId}: malformed HaikuResult JSON — treating as blocked.",
                scenarioId);
            return BlockedEntry(scenarioId, batchId, haikuId);
        }

        var validation = SchemaValidator.Validate(text, Schema.HaikuResult);
        if (!validation.IsValid)
        {
            _log.LogWarning(
                "Scenario {ScenarioId}: HaikuResult failed schema validation ({Errors}) — treating as blocked.",
                scenarioId, string.Join("; ", validation.Errors));
            return BlockedEntry(scenarioId, batchId, haikuId);
        }

        // Stamp the parsed result with authoritative server-side values. The model emits
        // a role-frame placeholder ("batch-pending", zero token counts); the orchestrator
        // is the only authority for the real batchId and the real token usage from the
        // batch API response. Without this overwrite the report aggregator can't join
        // Haikus to their parent Sonnet (placeholder ParentBatchId never matches the
        // Sonnet's scenarioBatch.batchId), and the cost ledger reports zero spend.
        var u = succeeded.Message.Usage;
        return parsed with
        {
            ParentBatchId = batchId,
            TokensUsed    = new TokenUsage
            {
                Input      = u.InputTokens,
                CachedRead = u.CacheReadInputTokens,
                CacheWrite = u.CacheCreationInputTokens,
                Output     = u.OutputTokens
            }
        };
    }

    private static HaikuResult BlockedEntry(string scenarioId, string batchId, string haikuId)
        => new()
        {
            SchemaVersion    = "0.1.0",
            ScenarioId       = scenarioId,
            ParentBatchId    = batchId,
            WorkerId         = haikuId,
            Outcome          = OutcomeCode.Blocked,
            BlockReason      = BlockReason.ToolError,
            AssertionResults = new List<AssertionResult>(),
            TokensUsed       = new TokenUsage { Input = 0, CachedRead = 0, Output = 0, CacheWrite = 0 }
        };

    private static LedgerEntry BuildLedgerEntry(string runId, string haikuId, HaikuResult result)
    {
        var rates  = CostRates.Haiku;
        var tokens = result.TokensUsed;

        var usdIn  = tokens.Input      / 1_000_000m * rates.BatchInputPerMtok;
        var usdCR  = tokens.CachedRead / 1_000_000m * rates.CacheReadPerMtok;
        var usdCW  = tokens.CacheWrite / 1_000_000m * rates.CacheWritePerMtok;
        var usdOut = tokens.Output     / 1_000_000m * rates.BatchOutputPerMtok;

        return new LedgerEntry
        {
            RunId            = runId,
            Tier             = "haiku",
            WorkerId         = haikuId,
            Model            = ModelId.HaikuV45.Name,
            InputTokens      = tokens.Input,
            CachedReadTokens = tokens.CachedRead,
            CacheWriteTokens = tokens.CacheWrite,
            OutputTokens     = tokens.Output,
            UsdInput         = usdIn,
            UsdCachedRead    = usdCR,
            UsdCacheWrite    = usdCW,
            UsdOutput        = usdOut,
            UsdTotal         = usdIn + usdCR + usdCW + usdOut,
            Timestamp        = DateTimeOffset.UtcNow
        };
    }
}
