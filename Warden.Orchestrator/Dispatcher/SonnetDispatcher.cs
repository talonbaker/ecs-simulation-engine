using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Warden.Anthropic;
using Warden.Contracts;
using Warden.Contracts.Handshake;
using Warden.Contracts.SchemaValidation;
using Warden.Orchestrator.Cache;
using Warden.Orchestrator.Infrastructure;
using InlineOutcome = Warden.Orchestrator.Dispatcher.InlineReferenceFiles.Outcome;

namespace Warden.Orchestrator.Dispatcher;

/// <summary>
/// Executes one Sonnet call end-to-end:
/// assemble prompt → call Anthropic (with Polly) → validate →
/// record cost → persist CoT → return <see cref="SonnetResult"/>.
/// </summary>
public sealed class SonnetDispatcher
{
    private readonly IAnthropicClient                    _client;
    private readonly PromptCacheManager                  _cache;
    private readonly IChainOfThoughtStore                _cot;
    private readonly CostLedger                          _ledger;
    private readonly Persistence.BudgetGuard             _budget;
    private readonly ResiliencePipeline<MessageResponse> _retry;
    private readonly ILogger<SonnetDispatcher>           _log;
    private readonly IWorktreeDiffSource                 _diffSource;
    private readonly string                              _repoRoot;

    public SonnetDispatcher(
        IAnthropicClient                    client,
        PromptCacheManager                  cache,
        IChainOfThoughtStore                cot,
        CostLedger                          ledger,
        Persistence.BudgetGuard             budget,
        ResiliencePipeline<MessageResponse> retry,
        ILogger<SonnetDispatcher>           log,
        IWorktreeDiffSource                 diffSource,
        string?                             repoRoot = null)
    {
        _client     = client;
        _cache      = cache;
        _cot        = cot;
        _ledger     = ledger;
        _budget     = budget;
        _retry      = retry;
        _log        = log;
        _diffSource = diffSource;
        _repoRoot   = repoRoot ?? Environment.CurrentDirectory;
    }

    /// <summary>
    /// Runs one Sonnet call for the given <paramref name="spec"/>.
    /// Returns a <see cref="SonnetResult"/> — never throws on schema or API errors;
    /// those become <c>outcome=blocked</c>.
    /// </summary>
    public async Task<SonnetResult> RunAsync(
        string            runId,
        string            workerId,
        OpusSpecPacket    spec,
        bool              dryRun,
        CancellationToken ct)
    {
        var specJson = JsonSerializer.Serialize(spec, JsonOptions.Wire);

        // Inline-files preprocessing: pre-read every path listed in spec.Inputs.ReferenceFiles
        // and prepend the formatted block to the user turn.  Sonnets dispatched via the API
        // have no file-system access; without this step any spec with referenceFiles always
        // blocks with missing-reference-file (PHASE-1-HANDOFF §4 / WP-2.0.A).
        string userTurnBody = specJson;
        if (spec.Inputs?.ReferenceFiles?.Count > 0)
        {
            InlineOutcome inlineResult = InlineReferenceFiles.Build(spec.Inputs.ReferenceFiles, _repoRoot);
            if (inlineResult.Reason is not null)
            {
                var blocked = MakeBlockedResult(spec.SpecId, workerId,
                    inlineResult.Reason.Value, inlineResult.Details ?? string.Empty);
                if (!dryRun)
                {
                    var blockedJson = JsonSerializer.Serialize(blocked, JsonOptions.Wire);
                    await _cot.PersistResultAsync(runId, workerId, blockedJson, ct).ConfigureAwait(false);
                    await _cot.AppendEventAsync(runId,
                        BuildInlineBlockedEventJson(workerId, spec.SpecId, inlineResult.Reason.Value),
                        ct).ConfigureAwait(false);
                }
                return blocked;
            }
            if (inlineResult.InlinedBlock is not null)
                userTurnBody = inlineResult.InlinedBlock + specJson;
        }

        var request = _cache.BuildRequest(ModelId.SonnetV46, userTurnBody);

        if (dryRun)
        {
            Console.WriteLine($"[dry-run] {workerId} prompt for spec '{spec.SpecId}':");
            foreach (var block in request.System ?? new List<ContentBlock>())
                if (block is TextBlock tb) Console.WriteLine(tb.Text);
            Console.WriteLine(userTurnBody);
            return MakeStubResult(spec.SpecId, workerId, OutcomeCode.Ok);
        }

        var budgetVerdict = _budget.Check(projectedCostUsd: 0.50m);
        if (!budgetVerdict.CanProceed)
        {
            _log.LogWarning("Budget exceeded before dispatching {WorkerId}.", workerId);
            return MakeBlockedResult(spec.SpecId, workerId, BlockReason.BudgetExceeded, "Budget exceeded.");
        }

        await _cot.PersistSpecAsync(runId, workerId, specJson, ct).ConfigureAwait(false);

        var promptText = JsonSerializer.Serialize(request, JsonOptions.Wire);
        await _cot.PersistPromptAsync(runId, workerId, promptText, ct).ConfigureAwait(false);

        MessageResponse response;
        try
        {
            response = await _retry.ExecuteAsync(
                async token => await _client.CreateMessageAsync(request, token).ConfigureAwait(false),
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "{WorkerId}: API call failed after retries.", workerId);
            return MakeBlockedResult(spec.SpecId, workerId, BlockReason.ToolError, ex.Message);
        }

        var rawResponseJson = JsonSerializer.Serialize(response, JsonOptions.Wire);
        await _cot.PersistRawResponseAsync(runId, workerId, rawResponseJson, ct).ConfigureAwait(false);

        var text = response.Content.OfType<TextBlock>().FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return MakeBlockedResult(spec.SpecId, workerId, BlockReason.SchemaMismatchOnOwnOutput,
                "Response contained no text block.");
        }

        // Defensive: strip any prose/markdown wrapping. The role frame instructs the model
        // to return raw JSON, but if it wraps in ```json fences or adds preamble, extract
        // the JSON object between the first '{' and the last '}' before parsing.
        text = ExtractJsonObject(text);

        SonnetResult result;
        try
        {
            result = JsonSerializer.Deserialize<SonnetResult>(text, JsonOptions.Wire)
                ?? throw new JsonException("Deserialised SonnetResult was null.");
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "{WorkerId}: malformed SonnetResult JSON.", workerId);
            return MakeBlockedResult(spec.SpecId, workerId, BlockReason.SchemaMismatchOnOwnOutput, ex.Message);
        }

        var validation = SchemaValidator.Validate(text, Schema.SonnetResult);
        if (!validation.IsValid)
        {
            _log.LogWarning("{WorkerId}: SonnetResult failed schema validation: {Errors}",
                workerId, string.Join("; ", validation.Errors));
            return MakeBlockedResult(spec.SpecId, workerId, BlockReason.SchemaMismatchOnOwnOutput,
                string.Join("; ", validation.Errors));
        }

        // Stamp the parsed result with authoritative server-side token usage. The model
        // self-reports placeholder values that don't match what Anthropic actually billed.
        // The cost ledger pulls from response.Usage directly; the persisted result.json
        // would otherwise diverge from the ledger and from Anthropic Console.
        var usage = response.Usage;
        result = result with
        {
            TokensUsed = new Contracts.Handshake.TokenUsage
            {
                Input      = usage.InputTokens,
                CachedRead = usage.CacheReadInputTokens,
                CacheWrite = usage.CacheCreationInputTokens,
                Output     = usage.OutputTokens
            }
        };

        // Retrieve the worktree diff now (before ledger/CoT), then evaluate.
        // The override is applied after the ledger write so the spend is always recorded.
        var diff    = await _diffSource.GetDiffAsync(result.WorktreePath, ct).ConfigureAwait(false);
        var verdict = FailClosedEscalator.Evaluate(result, diff);

        var rates = CostRates.Sonnet;
        var usdIn  = usage.InputTokens                / 1_000_000m * rates.InputPerMtok;
        var usdCR  = usage.CacheReadInputTokens       / 1_000_000m * rates.CacheReadPerMtok;
        var usdCW  = usage.CacheCreationInputTokens   / 1_000_000m * rates.CacheWritePerMtok;
        var usdOut = usage.OutputTokens               / 1_000_000m * rates.OutputPerMtok;
        var usdTotal = usdIn + usdCR + usdCW + usdOut;

        _budget.Record(usdTotal);

        var entry = new LedgerEntry
        {
            RunId            = runId,
            Tier             = "sonnet",
            WorkerId         = workerId,
            Model            = ModelId.SonnetV46.Name,
            InputTokens      = usage.InputTokens,
            CachedReadTokens = usage.CacheReadInputTokens,
            CacheWriteTokens = usage.CacheCreationInputTokens,
            OutputTokens     = usage.OutputTokens,
            UsdInput         = usdIn,
            UsdCachedRead    = usdCR,
            UsdCacheWrite    = usdCW,
            UsdOutput        = usdOut,
            UsdTotal         = usdTotal,
            Timestamp        = DateTimeOffset.UtcNow
        };
        await _ledger.AppendAsync(entry, ct).ConfigureAwait(false);

        var resultJson = JsonSerializer.Serialize(result, JsonOptions.Wire);
        await _cot.PersistResultAsync(runId, workerId, resultJson, ct).ConfigureAwait(false);

        // Apply banned-pattern override after ledger (the spend was recorded; only the return value changes).
        if (result.Outcome == OutcomeCode.Ok && verdict.TerminalOutcome == OutcomeCode.Blocked)
        {
            _log.LogInformation("{WorkerId}: banned pattern detected in worktree diff; overriding to blocked.", workerId);
            return MakeBlockedResult(spec.SpecId, workerId, BlockReason.ToolError, verdict.HumanMessage);
        }

        _log.LogInformation("{WorkerId}: completed, outcome={Outcome}.", workerId, result.Outcome);
        return result;
    }

    // -- Helpers ------------------------------------------------------------------

    /// <summary>
    /// Strips prose and markdown code fences from a Sonnet response, extracting the
    /// JSON object between the first '{' and the last '}'. Defensive — the role frame
    /// instructs the model to return raw JSON, but small models (or rare drift) may
    /// add preamble or wrap in ```json fences. Returns the original text unchanged if
    /// no balanced braces are found, letting downstream JSON parsing fail with the
    /// original error message.
    /// </summary>
    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return text;
        var end = text.LastIndexOf('}');
        if (end <= start) return text;
        return text.Substring(start, end - start + 1);
    }

    private static SonnetResult MakeStubResult(string specId, string workerId, OutcomeCode outcome)
        => new()
        {
            SchemaVersion        = "0.1.0",
            SpecId               = specId,
            WorkerId             = workerId,
            Outcome              = outcome,
            AcceptanceTestResults = new List<AcceptanceTestResult>(),
            TokensUsed           = new Contracts.Handshake.TokenUsage()
        };

    private static SonnetResult MakeBlockedResult(
        string specId, string workerId, BlockReason reason, string details)
        => new()
        {
            SchemaVersion         = "0.1.0",
            SpecId                = specId,
            WorkerId              = workerId,
            Outcome               = OutcomeCode.Blocked,
            BlockReason           = reason,
            BlockDetails          = details,
            AcceptanceTestResults = new List<AcceptanceTestResult>(),
            TokensUsed            = new Contracts.Handshake.TokenUsage()
        };

    private static string BuildInlineBlockedEventJson(
        string workerId, string specId, BlockReason reason)
    {
        // Serialise the reason enum to its kebab-case wire form, then strip the surrounding quotes.
        var reasonStr = JsonSerializer.Serialize(reason, JsonOptions.Wire).Trim('"');
        var ts = DateTimeOffset.UtcNow.ToString("O");
        return $"{{\"ts\":\"{ts}\",\"kind\":\"inline-files-blocked\",\"workerId\":\"{workerId}\",\"reason\":\"{reasonStr}\",\"specId\":\"{specId}\"}}";
    }
}
