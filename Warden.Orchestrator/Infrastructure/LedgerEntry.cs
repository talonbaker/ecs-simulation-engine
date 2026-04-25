namespace Warden.Orchestrator.Infrastructure;

/// <summary>
/// One line of the cost-ledger JSONL. Matches the shape described in SRD §2 Pillar D.5.
/// </summary>
public sealed record LedgerEntry
{
    public string         RunId            { get; init; } = string.Empty;
    public string         Tier             { get; init; } = string.Empty;
    public string         WorkerId         { get; init; } = string.Empty;
    public string         Model            { get; init; } = string.Empty;
    public int            InputTokens      { get; init; }
    public int            CachedReadTokens { get; init; }
    public int            CacheWriteTokens { get; init; }
    public int            OutputTokens     { get; init; }
    public decimal        UsdInput         { get; init; }
    public decimal        UsdCachedRead    { get; init; }
    public decimal        UsdCacheWrite    { get; init; }
    public decimal        UsdOutput        { get; init; }
    public decimal        UsdTotal         { get; init; }
    public DateTimeOffset Timestamp        { get; init; }
}
