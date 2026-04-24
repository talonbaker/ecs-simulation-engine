using System.Collections.Generic;
using System.Text.Json;

namespace Warden.Contracts.Handshake;

/// <summary>
/// Verdict returned by one Tier-3 Haiku on one scenario.
/// Parsed and rolled up per Sonnet by the orchestrator.
/// Schema: <c>haiku-result.schema.json</c> v0.1.0.
/// </summary>
public sealed record HaikuResult
{
    public string              SchemaVersion    { get; init; } = "0.1.0";
    public string              ScenarioId       { get; init; } = string.Empty;
    public string              ParentBatchId    { get; init; } = string.Empty;
    public string              WorkerId         { get; init; } = string.Empty;
    public OutcomeCode         Outcome          { get; init; }
    public List<AssertionResult> AssertionResults { get; init; } = new();
    public TokenUsage          TokensUsed       { get; init; } = default!;

    // Required only when outcome == blocked
    public BlockReason?   BlockReason     { get; init; }

    // Optional
    public Dictionary<string, double>? KeyMetrics      { get; init; }
    public TelemetryDigest?            TelemetryDigest { get; init; }
    public string?                     Note            { get; init; }
}

public sealed record AssertionResult
{
    public string  Id      { get; init; } = string.Empty;
    public bool    Passed  { get; init; }

    // Optional
    public JsonElement? ObservedValue          { get; init; }
    public double?      ObservedAtGameSeconds  { get; init; }
    public string?      Note                   { get; init; }
}

public sealed record TelemetryDigest
{
    public int?    TotalTicks        { get; init; }
    public int?    ViolationCount    { get; init; }
    public string? FinalTimeDisplay  { get; init; }
    public int?    EntityCount       { get; init; }
}
