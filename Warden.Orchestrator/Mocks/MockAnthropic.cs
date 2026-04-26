using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Warden.Anthropic;
using Warden.Contracts;
using Warden.Contracts.Handshake;
using AnthropicTokenUsage = Warden.Anthropic.TokenUsage;
using ContractTokenUsage  = Warden.Contracts.Handshake.TokenUsage;

namespace Warden.Orchestrator.Mocks;

/// <summary>
/// In-process replacement for <see cref="AnthropicClient"/> used when <c>--mock-anthropic</c>
/// is passed. Returns canned <see cref="SonnetResult"/> and <see cref="HaikuResult"/> values
/// from <see cref="MockCannedResponses"/> without any network calls.
/// </summary>
public sealed class MockAnthropic : IAnthropicClient
{
    private readonly MockCannedResponses _responses;

    private static int _batchCounter;
    private readonly Dictionary<string, List<string>> _batchScenarios = new(StringComparer.Ordinal);

    public MockAnthropic(MockCannedResponses responses)
    {
        _responses = responses;
    }

    /// <inheritdoc/>
    public Task<MessageResponse> CreateMessageAsync(
        MessageRequest request,
        CancellationToken ct = default)
    {
        var userText = request.Messages
            .FirstOrDefault()
            ?.Content.OfType<TextBlock>().FirstOrDefault()
            ?.Text ?? string.Empty;

        var specId = ExtractSpecId(userText) ?? "unknown";
        var sonnetResult = _responses.GetSonnetResult(specId);

        var resultJson = JsonSerializer.Serialize(sonnetResult, JsonOptions.Wire);

        var tokens = sonnetResult.TokensUsed;
        var response = new MessageResponse(
            Id:            "mock-msg-" + Guid.NewGuid().ToString("N")[..8],
            Type:          "message",
            Role:          "assistant",
            Content:       new List<ContentBlock> { new TextBlock(resultJson) },
            Model:         ModelId.SonnetV46,
            StopReason:    "end_turn",
            StopSequence:  null,
            Usage:         new AnthropicTokenUsage(
                               InputTokens:                tokens.Input,
                               CacheCreationInputTokens:   tokens.CacheWrite,
                               CacheReadInputTokens:       tokens.CachedRead,
                               OutputTokens:               tokens.Output));

        return Task.FromResult(response);
    }

    /// <inheritdoc/>
    public Task<BatchSubmission> CreateBatchAsync(
        BatchRequest request,
        CancellationToken ct = default)
    {
        var seq     = Interlocked.Increment(ref _batchCounter);
        var batchId = $"batch-mock-{seq:D2}";

        lock (_batchScenarios)
        {
            _batchScenarios[batchId] = request.Requests
                .Select(r => r.CustomId)
                .ToList();
        }

        var submission = new BatchSubmission(
            Id:               batchId,
            Type:             "message_batch",
            ProcessingStatus: "ended",
            CreatedAt:        DateTimeOffset.UtcNow,
            ResultsUrl:       $"mock://results/{batchId}");

        return Task.FromResult(submission);
    }

    /// <inheritdoc/>
    public Task<BatchStatus> GetBatchAsync(
        string batchId,
        CancellationToken ct = default)
    {
        int count;
        lock (_batchScenarios)
        {
            count = _batchScenarios.TryGetValue(batchId, out var ids) ? ids.Count : 0;
        }

        var status = new BatchStatus(
            Id:               batchId,
            ProcessingStatus: BatchProcessingStatus.Ended,
            RequestCounts:    new BatchRequestCounts(0, count, 0, 0, 0),
            EndedAt:          DateTimeOffset.UtcNow,
            CreatedAt:        DateTimeOffset.UtcNow,
            ExpiresAt:        DateTimeOffset.UtcNow.AddHours(24),
            ResultsUrl:       $"mock://results/{batchId}");

        return Task.FromResult(status);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<BatchResultEntry> StreamBatchResultsAsync(
        string batchId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        List<string>? customIds;
        lock (_batchScenarios)
        {
            _batchScenarios.TryGetValue(batchId, out customIds);
        }

        if (customIds is null) yield break;

        foreach (var customId in customIds)
        {
            ct.ThrowIfCancellationRequested();

            // custom_id is a composite "<batchId>::<scenarioId>"; strip the prefix so the
            // canned-response lookup uses the plain scenarioId that the fixture files use.
            var scenarioId = ExtractScenarioId(customId);
            var haiku      = _responses.GetHaikuResult(scenarioId) with { ParentBatchId = batchId };
            var haiJson    = JsonSerializer.Serialize(haiku, JsonOptions.Wire);

            var tokens = haiku.TokensUsed;
            var msg = new MessageResponse(
                Id:           "mock-haiku-" + Guid.NewGuid().ToString("N")[..8],
                Type:         "message",
                Role:         "assistant",
                Content:      new List<ContentBlock> { new TextBlock(haiJson) },
                Model:        ModelId.HaikuV45,
                StopReason:   "end_turn",
                StopSequence: null,
                Usage:        new AnthropicTokenUsage(
                                  InputTokens:                tokens.Input,
                                  CacheCreationInputTokens:   tokens.CacheWrite,
                                  CacheReadInputTokens:       tokens.CachedRead,
                                  OutputTokens:               tokens.Output));

            yield return new BatchResultEntry(
                CustomId: customId,   // echo the original composite id back verbatim
                Result:   new SucceededResult(msg));
        }
    }

    private static string ExtractScenarioId(string customId)
    {
        var idx = customId.LastIndexOf("::", StringComparison.Ordinal);
        return idx >= 0 ? customId[(idx + 2)..] : customId;
    }

    // -- Helpers ------------------------------------------------------------------

    private static readonly Regex SpecIdPattern =
        new(@"""specId""\s*:\s*""(spec-[a-z0-9-]+)""", RegexOptions.Compiled);

    private static string? ExtractSpecId(string text)
    {
        var m = SpecIdPattern.Match(text);
        return m.Success ? m.Groups[1].Value : null;
    }
}
