using System.Text.Json.Serialization;
using Warden.Anthropic;

namespace Warden.Orchestrator.Persistence;

/// <summary>
/// One row in the cost ledger, written as a JSONL line per API call.
/// Shape matches SRD §2 Pillar D.5.
/// </summary>
public sealed record LedgerEntry(
    [property: JsonPropertyName("runId")]            string         RunId,
    [property: JsonPropertyName("tier")]             string         Tier,
    [property: JsonPropertyName("workerId")]         string         WorkerId,
    [property: JsonPropertyName("model")]            ModelId        Model,
    [property: JsonPropertyName("inputTokens")]      long           InputTokens,
    [property: JsonPropertyName("cachedReadTokens")] long           CachedReadTokens,
    [property: JsonPropertyName("cacheWriteTokens")] long           CacheWriteTokens,
    [property: JsonPropertyName("outputTokens")]     long           OutputTokens,
    [property: JsonPropertyName("usdInput")]         decimal        UsdInput,
    [property: JsonPropertyName("usdCachedRead")]    decimal        UsdCachedRead,
    [property: JsonPropertyName("usdCacheWrite")]    decimal        UsdCacheWrite,
    [property: JsonPropertyName("usdOutput")]        decimal        UsdOutput,
    [property: JsonPropertyName("usdTotal")]         decimal        UsdTotal,
    [property: JsonPropertyName("timestamp")]        DateTimeOffset Timestamp);
