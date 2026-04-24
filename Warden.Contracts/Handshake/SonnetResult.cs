using System.Collections.Generic;

namespace Warden.Contracts.Handshake;

/// <summary>
/// What a Tier-2 Sonnet returns when it finishes one SpecPacket.
/// Parsed and validated by the orchestrator before any downstream dispatch.
/// Schema: <c>sonnet-result.schema.json</c> v0.1.0.
/// </summary>
public sealed record SonnetResult
{
    public string              SchemaVersion        { get; init; } = "0.1.0";
    public string              SpecId               { get; init; } = string.Empty;
    public string              WorkerId             { get; init; } = string.Empty;
    public OutcomeCode         Outcome              { get; init; }
    public List<AcceptanceTestResult> AcceptanceTestResults { get; init; } = new();
    public TokenUsage          TokensUsed           { get; init; } = default!;

    // Required only when outcome == blocked
    public BlockReason?  BlockReason  { get; init; }
    public string?       BlockDetails { get; init; }

    // Optional
    public string?       WorktreePath  { get; init; }
    public DiffSummary?  DiffSummary   { get; init; }
    public ScenarioBatch? ScenarioBatch { get; init; }
    public string?       Notes         { get; init; }
}

public sealed record AcceptanceTestResult
{
    public string  Id       { get; init; } = string.Empty;
    public bool    Passed   { get; init; }
    public string? Evidence { get; init; }
}

public sealed record DiffSummary
{
    public int FilesAdded    { get; init; }
    public int FilesModified { get; init; }
    public int FilesDeleted  { get; init; }
    public int LinesAdded    { get; init; }
    public int LinesRemoved  { get; init; }
}

public sealed record TokenUsage
{
    public int Input       { get; init; }
    public int CachedRead  { get; init; }
    public int Output      { get; init; }
    public int CacheWrite  { get; init; }
}
