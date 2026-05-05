using System.Text;
using System.Text.Json;
using Warden.Anthropic;
using Warden.Contracts;
using Warden.Contracts.Handshake;
using InfraLedgerEntry = Warden.Orchestrator.Infrastructure.LedgerEntry;

namespace Warden.Orchestrator.Reports;

/// <summary>
/// Reads an existing run directory and produces an in-memory <see cref="Report"/>,
/// then writes <c>report.md</c> and <c>report.json</c> via the two writer helpers.
/// </summary>
public sealed class ReportAggregator
{
    private readonly string  _runsRoot;
    private readonly decimal _budgetUsd;

    public ReportAggregator(string runsRoot, decimal budgetUsd = decimal.MaxValue)
    {
        _runsRoot  = runsRoot;
        _budgetUsd = budgetUsd;
    }

    public Report Build(string runId)
    {
        var runRoot = Path.Combine(_runsRoot, runId);
        var events  = ReadEvents(runRoot);
        var ledger  = ReadLedger(runRoot);

        var sonnetDirs = Directory.Exists(runRoot)
            ? Directory.GetDirectories(runRoot, "sonnet-*").OrderBy(d => d).ToArray()
            : Array.Empty<string>();

        var haikuDirs = Directory.Exists(runRoot)
            ? Directory.GetDirectories(runRoot, "haiku-*", SearchOption.AllDirectories)
            : Array.Empty<string>();

        var haikuByWorkerId = BuildHaikuMap(haikuDirs);
        var haikuByBatchId  = BuildHaikuByBatchId(haikuByWorkerId);

        string missionId       = runId;
        var    sonnetSections  = new List<SonnetSection>();

        foreach (var dir in sonnetDirs)
        {
            var spec   = TryReadSpec(dir);
            var result = TryReadSonnetResult(dir);
            if (result is null) continue;

            if (spec is { MissionId.Length: > 0 }) missionId = spec.MissionId;

            var workerId = Path.GetFileName(dir);
            var spend    = ledger.Where(e => e.WorkerId == workerId).Sum(e => e.UsdTotal);
            var notes    = BuildSonnetNotes(result);

            var haikus = new List<HaikuSection>();
            if (result.ScenarioBatch is { BatchId: { } batchId })
            {
                if (haikuByBatchId.TryGetValue(batchId, out var haikuIds))
                {
                    foreach (var hId in haikuIds.OrderBy(x => x))
                    {
                        if (!haikuByWorkerId.TryGetValue(hId, out var h)) continue;
                        haikus.Add(BuildHaikuSection(h.Result, h.Scenario, ledger));
                    }
                }
            }

            var section = new SonnetSection
            {
                SpecId       = result.SpecId,
                SpecTitle    = spec?.Title ?? string.Empty,
                WorkerId     = workerId,
                Outcome      = result.Outcome.ToString().ToLowerInvariant(),
                AtsPassed    = result.AcceptanceTestResults.Count(at => at.Passed),
                AtsTotal     = result.AcceptanceTestResults.Count,
                SpendUsd     = spend,
                Notes        = notes,
                WorktreePath = result.WorktreePath,
                Diff         = result.DiffSummary is { } ds
                    ? new DiffData
                    {
                        FilesAdded    = ds.FilesAdded,
                        FilesModified = ds.FilesModified,
                        FilesDeleted  = ds.FilesDeleted,
                        LinesAdded    = ds.LinesAdded,
                        LinesRemoved  = ds.LinesRemoved
                    }
                    : null,
                AtResults = result.AcceptanceTestResults
                    .Select(at => new AcceptanceTestRow
                    {
                        Id     = at.Id,
                        Passed = at.Passed,
                        Notes  = at.Evidence
                    })
                    .ToList(),
                Haikus = haikus
            };
            sonnetSections.Add(section);
        }

        var cost       = BuildCostSummary(ledger);
        var notableEvt = BuildNotableEvents(events);
        var startTime  = GetStartTime(events);
        var endTime    = GetEndTime(events);
        var outcome    = GetTerminalOutcome(events, sonnetSections);
        var exitCode   = GetExitCode(events);
        var earlyTerm  = !events.Any(e => e.Kind == "run-completed");
        var totalSpend = ledger.Sum(e => e.UsdTotal);

        return new Report
        {
            RunId              = runId,
            MissionId          = missionId,
            StartedUtc         = startTime,
            EndedUtc           = endTime,
            TerminalOutcome    = outcome,
            ExitCode           = exitCode,
            TotalSpendUsd      = totalSpend,
            BudgetUsd          = _budgetUsd,
            IsEarlyTermination = earlyTerm,
            Sonnets            = sonnetSections,
            Cost               = cost,
            Events             = notableEvt
        };
    }

    public void Emit(Report report, string rootDir)
    {
        Directory.CreateDirectory(rootDir);
        File.WriteAllText(
            Path.Combine(rootDir, "report.md"),
            MarkdownReportWriter.Render(report),
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(rootDir, "report.json"),
            JsonReportWriter.Render(report),
            Encoding.UTF8);
    }

    // -- Private helpers -----------------------------------------------------------

    private sealed record EventLine(DateTimeOffset Timestamp, string Kind, string Raw);
    private sealed record HaikuData(ScenarioDto? Scenario, HaikuResult Result);

    private static List<EventLine> ReadEvents(string runRoot)
    {
        var path = Path.Combine(runRoot, "events.jsonl");
        if (!File.Exists(path)) return new List<EventLine>();

        var result = new List<EventLine>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc  = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var ts   = root.TryGetProperty("ts", out var tsProp)
                    ? DateTimeOffset.Parse(tsProp.GetString()!)
                    : DateTimeOffset.MinValue;
                var kind = root.TryGetProperty("kind", out var kindProp)
                    ? kindProp.GetString() ?? "unknown"
                    : "unknown";
                result.Add(new EventLine(ts, kind, line));
            }
            catch { /* skip malformed lines */ }
        }
        return result;
    }

    private static List<InfraLedgerEntry> ReadLedger(string runRoot)
    {
        var path = Path.Combine(runRoot, "cost-ledger.jsonl");
        if (!File.Exists(path)) return new List<InfraLedgerEntry>();

        var result = new List<InfraLedgerEntry>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var entry = JsonSerializer.Deserialize<InfraLedgerEntry>(line, JsonOptions.Wire);
                if (entry is not null) result.Add(entry);
            }
            catch { /* skip malformed lines */ }
        }
        return result;
    }

    private static OpusSpecPacket? TryReadSpec(string sonnetDir)
    {
        var path = Path.Combine(sonnetDir, "spec.json");
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<OpusSpecPacket>(
                File.ReadAllText(path), JsonOptions.Wire);
        }
        catch { return null; }
    }

    private static SonnetResult? TryReadSonnetResult(string sonnetDir)
    {
        var path = Path.Combine(sonnetDir, "result.json");
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<SonnetResult>(
                File.ReadAllText(path), JsonOptions.Wire);
        }
        catch { return null; }
    }

    private static Dictionary<string, HaikuData> BuildHaikuMap(string[] haikuDirs)
    {
        var map = new Dictionary<string, HaikuData>(StringComparer.Ordinal);
        foreach (var dir in haikuDirs)
        {
            var resultPath = Path.Combine(dir, "result.json");
            if (!File.Exists(resultPath)) continue;
            try
            {
                var result = JsonSerializer.Deserialize<HaikuResult>(
                    File.ReadAllText(resultPath), JsonOptions.Wire);
                if (result is null) continue;

                ScenarioDto? scenario = null;
                var scenPath = Path.Combine(dir, "scenario.json");
                if (File.Exists(scenPath))
                {
                    try
                    {
                        scenario = JsonSerializer.Deserialize<ScenarioDto>(
                            File.ReadAllText(scenPath), JsonOptions.Wire);
                    }
                    catch { }
                }

                map[result.WorkerId] = new HaikuData(scenario, result);
            }
            catch { }
        }
        return map;
    }

    private static Dictionary<string, List<string>> BuildHaikuByBatchId(
        Dictionary<string, HaikuData> haikuMap)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (workerId, data) in haikuMap)
        {
            var batchId = data.Result.ParentBatchId;
            if (!map.TryGetValue(batchId, out var list))
            {
                list = new List<string>();
                map[batchId] = list;
            }
            list.Add(workerId);
        }
        return map;
    }

    private static HaikuSection BuildHaikuSection(
        HaikuResult             result,
        ScenarioDto?            scenario,
        List<InfraLedgerEntry>  ledger)
    {
        var entry = ledger.FirstOrDefault(e => e.WorkerId == result.WorkerId);
        decimal spend;
        if (entry is not null)
        {
            spend = entry.UsdTotal;
        }
        else
        {
            var t = result.TokensUsed;
            var r = CostRates.Haiku;
            spend = t.Input      / 1_000_000m * r.BatchInputPerMtok
                  + t.CachedRead / 1_000_000m * r.CacheReadPerMtok
                  + t.CacheWrite / 1_000_000m * r.CacheWritePerMtok
                  + t.Output     / 1_000_000m * r.BatchOutputPerMtok;
        }

        return new HaikuSection
        {
            ScenarioId          = result.ScenarioId,
            Seed                = scenario?.Seed ?? 0,
            Outcome             = result.Outcome.ToString().ToLowerInvariant(),
            AssertionsPassed    = result.AssertionResults.Count(a => a.Passed),
            AssertionsTotal     = result.AssertionResults.Count,
            DurationGameSeconds = scenario?.DurationGameSeconds ?? 0,
            SpendUsd            = spend,
            KeyMetrics          = result.KeyMetrics
        };
    }

    private static CostSummary BuildCostSummary(List<InfraLedgerEntry> ledger)
    {
        var sonnetEntries = ledger.Where(e => e.Tier == "sonnet").ToList();
        var haikuEntries  = ledger.Where(e => e.Tier == "haiku").ToList();

        return new CostSummary
        {
            Sonnet = new TierCost
            {
                Calls            = sonnetEntries.Count,
                InputTokens      = sonnetEntries.Sum(e => (long)e.InputTokens),
                CachedReadTokens = sonnetEntries.Sum(e => (long)e.CachedReadTokens),
                OutputTokens     = sonnetEntries.Sum(e => (long)e.OutputTokens),
                SpendUsd         = sonnetEntries.Sum(e => e.UsdTotal)
            },
            Haiku = new TierCost
            {
                Calls            = haikuEntries.Count,
                InputTokens      = haikuEntries.Sum(e => (long)e.InputTokens),
                CachedReadTokens = haikuEntries.Sum(e => (long)e.CachedReadTokens),
                OutputTokens     = haikuEntries.Sum(e => (long)e.OutputTokens),
                SpendUsd         = haikuEntries.Sum(e => e.UsdTotal)
            }
        };
    }

    private static List<NotableEvent> BuildNotableEvents(List<EventLine> events)
        => events.Select(e =>
        {
            string? detail = null;
            try
            {
                using var doc  = JsonDocument.Parse(e.Raw);
                var root = doc.RootElement;
                detail = e.Kind switch
                {
                    "run-completed" => root.TryGetProperty("exitCode", out var ec)
                        ? $"exit={ec.GetInt32()}"
                        : null,
                    "sonnet-completed" => root.TryGetProperty("outcome", out var oc)
                        ? $"({oc.GetString()})"
                        : null,
                    "batch-submitted" => root.TryGetProperty("scenarioCount", out var sc)
                        ? $"({sc.GetInt32()} scenarios)"
                        : null,
                    _ => null
                };
            }
            catch { }
            return new NotableEvent { Timestamp = e.Timestamp, Kind = e.Kind, Detail = detail };
        }).ToList();

    private static DateTimeOffset GetStartTime(List<EventLine> events)
    {
        var e = events.FirstOrDefault(x => x.Kind == "run-started");
        return e?.Timestamp ?? DateTimeOffset.MinValue;
    }

    private static DateTimeOffset? GetEndTime(List<EventLine> events)
    {
        var e = events.FirstOrDefault(x => x.Kind == "run-completed");
        return e?.Timestamp;
    }

    private static string GetTerminalOutcome(List<EventLine> events, List<SonnetSection> sonnets)
    {
        var completed = events.FirstOrDefault(e => e.Kind == "run-completed");
        if (completed is null) return "blocked";

        return GetExitCodeFromEvent(completed) switch
        {
            0 => "ok",
            1 => "failed",
            3 => "budget-exceeded",
            _ => "blocked"
        };
    }

    private static int GetExitCode(List<EventLine> events)
    {
        var completed = events.FirstOrDefault(e => e.Kind == "run-completed");
        return completed is null ? 2 : GetExitCodeFromEvent(completed);
    }

    private static int GetExitCodeFromEvent(EventLine evt)
    {
        try
        {
            using var doc = JsonDocument.Parse(evt.Raw);
            if (doc.RootElement.TryGetProperty("exitCode", out var ec))
                return ec.GetInt32();
        }
        catch { }
        return 0;
    }

    private static string? BuildSonnetNotes(SonnetResult result)
    {
        if (result.Outcome == OutcomeCode.Ok) return null;

        if (result.Outcome == OutcomeCode.Failed)
        {
            var failing = result.AcceptanceTestResults
                .Where(at => !at.Passed)
                .Select(at => at.Id)
                .ToList();
            return failing.Count > 0
                ? string.Join(", ", failing) + " failed"
                : "tests failed";
        }

        return result.BlockReason?.ToString() ?? "blocked";
    }
}
