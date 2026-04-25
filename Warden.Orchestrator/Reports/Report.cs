namespace Warden.Orchestrator.Reports;

public sealed class Report
{
    public string          RunId              { get; init; } = string.Empty;
    public string          MissionId          { get; init; } = string.Empty;
    public DateTimeOffset  StartedUtc         { get; init; }
    public DateTimeOffset? EndedUtc           { get; init; }
    public string          TerminalOutcome    { get; init; } = "ok";
    public int             ExitCode           { get; init; }
    public decimal         TotalSpendUsd      { get; init; }
    public decimal         BudgetUsd          { get; init; }
    public bool            IsEarlyTermination { get; init; }

    public List<SonnetSection> Sonnets { get; init; } = new();
    public CostSummary         Cost    { get; init; } = new();
    public List<NotableEvent>  Events  { get; init; } = new();
}

public sealed class SonnetSection
{
    public string    SpecId       { get; init; } = string.Empty;
    public string    SpecTitle    { get; init; } = string.Empty;
    public string    WorkerId     { get; init; } = string.Empty;
    public string    Outcome      { get; init; } = string.Empty;
    public int       AtsPassed    { get; init; }
    public int       AtsTotal     { get; init; }
    public decimal   SpendUsd     { get; init; }
    public string?   Notes        { get; init; }
    public string?   WorktreePath { get; init; }
    public DiffData? Diff         { get; init; }

    public List<AcceptanceTestRow> AtResults { get; init; } = new();
    public List<HaikuSection>      Haikus    { get; init; } = new();
}

public sealed class DiffData
{
    public int FilesAdded    { get; init; }
    public int FilesModified { get; init; }
    public int FilesDeleted  { get; init; }
    public int LinesAdded    { get; init; }
    public int LinesRemoved  { get; init; }
}

public sealed class AcceptanceTestRow
{
    public string  Id     { get; init; } = string.Empty;
    public bool    Passed { get; init; }
    public string? Notes  { get; init; }
}

public sealed class HaikuSection
{
    public string  ScenarioId          { get; init; } = string.Empty;
    public int     Seed                { get; init; }
    public string  Outcome             { get; init; } = string.Empty;
    public int     AssertionsPassed    { get; init; }
    public int     AssertionsTotal     { get; init; }
    public double  DurationGameSeconds { get; init; }
    public decimal SpendUsd            { get; init; }

    public Dictionary<string, double>? KeyMetrics { get; init; }
}

public sealed class CostSummary
{
    public TierCost Sonnet { get; init; } = new();
    public TierCost Haiku  { get; init; } = new();
}

public sealed class TierCost
{
    public int     Calls            { get; init; }
    public long    InputTokens      { get; init; }
    public long    CachedReadTokens { get; init; }
    public long    OutputTokens     { get; init; }
    public decimal SpendUsd         { get; init; }
}

public sealed class NotableEvent
{
    public DateTimeOffset Timestamp { get; init; }
    public string         Kind      { get; init; } = string.Empty;
    public string?        Detail    { get; init; }
}
