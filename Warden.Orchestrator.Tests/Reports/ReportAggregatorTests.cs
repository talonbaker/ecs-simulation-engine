using System.Diagnostics;
using System.Text.Json;
using Warden.Contracts;
using Warden.Contracts.Handshake;
using Warden.Orchestrator.Reports;
using Xunit;

namespace Warden.Orchestrator.Tests.Reports;

/// <summary>
/// Unit tests for <see cref="ReportAggregator"/>, <see cref="MarkdownReportWriter"/>,
/// and <see cref="JsonReportWriter"/>. Each test builds a temporary run directory from
/// fixtures and verifies the produced <see cref="Report"/> and rendered files.
/// </summary>
public sealed class ReportAggregatorTests : IDisposable
{
    private readonly string _tempDir;

    public ReportAggregatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"wp12-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    // -- AT-01: Structure matches template headings and table columns ---------------

    [Fact]
    public void AT01_Render_ProducesAllRequiredSectionsAndTableColumns()
    {
        var runId = "run-at01";
        WriteSmokeMissionFixture(runId);
        var agg  = new ReportAggregator(_tempDir);
        var rep  = agg.Build(runId);
        var md   = MarkdownReportWriter.Render(rep);

        // Section headings (exact, fixed by WP-12)
        Assert.Contains("# Mission Report —",           md);
        Assert.Contains("## Summary",                   md);
        Assert.Contains("## Tier 2 — Sonnet results",   md);
        Assert.Contains("## Tier 3 — Haiku results",    md);
        Assert.Contains("## Cost summary",              md);
        Assert.Contains("## Notable events",            md);
        Assert.Contains("## Artifacts",                 md);

        // Sonnet table columns
        Assert.Contains("| Spec | Worker | Outcome | ATs passed | Spend | Notes |", md);

        // Haiku table columns
        Assert.Contains("| Scenario | Seed | Outcome | Assertions | Duration (gs) | Spend |", md);

        // Cost summary table columns
        Assert.Contains("| Tier | Calls | Input tok | Cached read tok | Output tok | Spend |", md);
    }

    // -- AT-02: Cost totals equal ledger sum ---------------------------------------

    [Fact]
    public void AT02_CostSummaryTotals_EqualLedgerSum_WithinOneCent()
    {
        var runId = "run-at02";
        WriteSmokeMissionFixture(runId);
        var agg = new ReportAggregator(_tempDir);
        var rep = agg.Build(runId);

        var ledgerTotal = rep.Cost.Sonnet.SpendUsd + rep.Cost.Haiku.SpendUsd;
        var diff        = Math.Abs(rep.TotalSpendUsd - ledgerTotal);
        Assert.True(diff <= 0.01m,
            $"Ledger tier totals {ledgerTotal:F6} differ from TotalSpendUsd {rep.TotalSpendUsd:F6} by {diff:F6}");
    }

    // -- AT-03: Failed outcome — Notes cites failing AT ----------------------------

    [Fact]
    public void AT03_FailedOutcome_NotesCitesFailingAcceptanceTest()
    {
        var runId = "run-at03";
        WriteRunRoot(runId, "run-started", addCompleted: true, exitCode: 1);

        // Sonnet result: failed, AT-02 did not pass
        var spec   = MakeSpec("spec-fail-01", "mission-fail");
        var result = MakeSonnetResult("spec-fail-01", "sonnet-01", OutcomeCode.Failed,
            new[]
            {
                new AcceptanceTestResult { Id = "AT-01", Passed = true  },
                new AcceptanceTestResult { Id = "AT-02", Passed = false }
            });

        WriteSonnetFiles(runId, "sonnet-01", spec, result);
        WriteLedgerEntry(runId, "sonnet", "sonnet-01", 0.05m, 1000, 5000, 500);

        var agg = new ReportAggregator(_tempDir);
        var rep = agg.Build(runId);

        Assert.Equal("failed", rep.TerminalOutcome);
        var s = Assert.Single(rep.Sonnets);
        Assert.Equal("failed", s.Outcome);
        Assert.NotNull(s.Notes);
        Assert.Contains("AT-02", s.Notes);
        Assert.Contains("failed", s.Notes);

        // MD should also carry the failing AT in the notes column
        var md = MarkdownReportWriter.Render(rep);
        Assert.Contains("AT-02", md);
    }

    // -- AT-04: Blocked mid-run produces partial report ----------------------------

    [Fact]
    public void AT04_BlockedMidRun_PartialReport_WithEarlyTerminationBanner()
    {
        var runId = "run-at04";
        // Write run-started but NO run-completed event
        WriteRunRoot(runId, "run-started", addCompleted: false);

        // sonnet-01: completed
        var spec1   = MakeSpec("spec-01", "mission-partial");
        var result1 = MakeSonnetResult("spec-01", "sonnet-01", OutcomeCode.Ok);
        WriteSonnetFiles(runId, "sonnet-01", spec1, result1);
        WriteLedgerEntry(runId, "sonnet", "sonnet-01", 0.03m, 800, 3000, 400);

        // sonnet-02: started but result.json does NOT exist (still running)
        Directory.CreateDirectory(Path.Combine(_tempDir, runId, "sonnet-02"));

        var agg = new ReportAggregator(_tempDir);
        var rep = agg.Build(runId);

        Assert.True(rep.IsEarlyTermination);
        Assert.Equal("blocked", rep.TerminalOutcome);

        // Only the completed worker should appear
        Assert.Single(rep.Sonnets);
        Assert.Equal("sonnet-01", rep.Sonnets[0].WorkerId);

        // MD must contain prominent early-termination notice
        var md = MarkdownReportWriter.Render(rep);
        Assert.Contains("Run terminated early", md);
    }

    // -- AT-05: report.json round-trips to the same Report -------------------------

    [Fact]
    public void AT05_ReportJson_RoundTrips_ToSameModel()
    {
        var runId = "run-at05";
        WriteSmokeMissionFixture(runId);
        var agg    = new ReportAggregator(_tempDir);
        var rep    = agg.Build(runId);
        var json   = JsonReportWriter.Render(rep);

        var restored = JsonSerializer.Deserialize<Report>(json, JsonOptions.Wire);
        Assert.NotNull(restored);

        Assert.Equal(rep.RunId,           restored!.RunId);
        Assert.Equal(rep.MissionId,       restored.MissionId);
        Assert.Equal(rep.TerminalOutcome, restored.TerminalOutcome);
        Assert.Equal(rep.ExitCode,        restored.ExitCode);
        Assert.Equal(rep.TotalSpendUsd,   restored.TotalSpendUsd);
        Assert.Equal(rep.Sonnets.Count,   restored.Sonnets.Count);
        Assert.Equal(rep.Cost.Sonnet.SpendUsd, restored.Cost.Sonnet.SpendUsd);
        Assert.Equal(rep.Cost.Haiku.SpendUsd,  restored.Cost.Haiku.SpendUsd);
        Assert.Equal(rep.Events.Count,    restored.Events.Count);
    }

    // -- AT-06: Emit adds less than 200ms wall-clock time -------------------------

    [Fact]
    public void AT06_Emit_CompletesInUnder200ms()
    {
        var runId = "run-at06";
        WriteSmokeMissionFixture(runId);
        var agg    = new ReportAggregator(_tempDir);
        var rep    = agg.Build(runId);
        var outDir = Path.Combine(_tempDir, "emit-at06");

        var sw = Stopwatch.StartNew();
        agg.Emit(rep, outDir);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 200,
            $"Emit took {sw.ElapsedMilliseconds} ms; expected < 200 ms.");

        Assert.True(File.Exists(Path.Combine(outDir, "report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "report.json")));
    }

    // -- Helpers -------------------------------------------------------------------

    private void WriteSmokeMissionFixture(string runId)
    {
        const string batchId = "batch-smoke-01";

        WriteRunRoot(runId, "run-started", addCompleted: true, exitCode: 0);

        // Sonnet spec + result with a ScenarioBatch
        var batch = new ScenarioBatch
        {
            BatchId      = batchId,
            ParentSpecId = "spec-smoke-01",
            Scenarios    = Enumerable.Range(1, 5)
                .Select(i => new ScenarioDto
                {
                    ScenarioId          = $"sc-{i:D2}",
                    Seed                = i * 10,
                    DurationGameSeconds = 3600,
                    Assertions          = new List<ScenarioAssertionDto>
                    {
                        new() { Id = "A-01", Kind = AssertionKind.AtEnd, Target = "entity-count" },
                        new() { Id = "A-02", Kind = AssertionKind.AtEnd, Target = "satiation"    },
                        new() { Id = "A-03", Kind = AssertionKind.Never, Target = "violation"    }
                    }
                })
                .ToList()
        };

        var spec   = MakeSpec("spec-smoke-01", "mission-smoke");
        var result = MakeSonnetResult(
            "spec-smoke-01", "sonnet-01", OutcomeCode.Ok,
            new[]
            {
                new AcceptanceTestResult { Id = "AT-01", Passed = true },
                new AcceptanceTestResult { Id = "AT-02", Passed = true },
                new AcceptanceTestResult { Id = "AT-03", Passed = true }
            },
            scenarioBatch: batch);

        WriteSonnetFiles(runId, "sonnet-01", spec, result);
        WriteLedgerEntry(runId, "sonnet", "sonnet-01", 0.061m, 2500, 32000, 1500);

        // Five Haiku results directly under runRoot (flat structure)
        int[] seeds = { 42, 99, 101, 1024, 65535 };
        for (int i = 0; i < 5; i++)
        {
            var haikuId    = $"haiku-{i + 1:D2}";
            var scenarioId = $"sc-{i + 1:D2}";
            var scenario   = batch.Scenarios[i];
            var haikuResult = MakeHaikuResult(scenarioId, batchId, haikuId, OutcomeCode.Ok,
                keyMetrics: new Dictionary<string, double>
                {
                    ["final satiation"] = 38.6 + i * 1.9,
                    ["violation count"] = 0
                });
            WriteHaikuFiles(runId, haikuId, scenario, haikuResult);
            WriteLedgerEntry(runId, "haiku", haikuId, 0.003m, 4000, 30000, 800);
        }
    }

    private void WriteRunRoot(
        string runId,
        string firstEventKind,
        bool   addCompleted,
        int    exitCode = 0)
    {
        var dir = Path.Combine(_tempDir, runId);
        Directory.CreateDirectory(dir);

        var events = new System.Text.StringBuilder();
        var ts1    = new DateTimeOffset(2026, 4, 23, 14, 23, 0, TimeSpan.Zero);
        events.AppendLine(
            $"{{\"ts\":\"{ts1:O}\",\"kind\":\"{firstEventKind}\",\"runId\":\"{runId}\"}}");

        if (addCompleted)
        {
            var ts2 = ts1.AddMinutes(3).AddSeconds(8);
            events.AppendLine(
                $"{{\"ts\":\"{ts2:O}\",\"kind\":\"run-completed\",\"exitCode\":{exitCode}}}");
        }

        File.WriteAllText(Path.Combine(dir, "events.jsonl"), events.ToString());
    }

    private void WriteSonnetFiles(
        string         runId,
        string         workerId,
        OpusSpecPacket spec,
        SonnetResult   result)
    {
        var dir = Path.Combine(_tempDir, runId, workerId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, "spec.json"),
            JsonSerializer.Serialize(spec, JsonOptions.Wire));
        File.WriteAllText(
            Path.Combine(dir, "result.json"),
            JsonSerializer.Serialize(result, JsonOptions.Wire));
    }

    private void WriteHaikuFiles(
        string      runId,
        string      haikuId,
        ScenarioDto scenario,
        HaikuResult result)
    {
        // Flat under runRoot — matches Infrastructure.ChainOfThoughtStore when
        // initialised correctly with runsRoot (not runDir).
        var dir = Path.Combine(_tempDir, runId, haikuId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, "scenario.json"),
            JsonSerializer.Serialize(scenario, JsonOptions.Wire));
        File.WriteAllText(
            Path.Combine(dir, "result.json"),
            JsonSerializer.Serialize(result, JsonOptions.Wire));
    }

    private void WriteLedgerEntry(
        string  runId,
        string  tier,
        string  workerId,
        decimal usdTotal,
        int     inputTok,
        int     cachedTok,
        int     outputTok)
    {
        var path  = Path.Combine(_tempDir, runId, "cost-ledger.jsonl");
        var entry = new
        {
            runId,
            tier,
            workerId,
            model            = tier == "sonnet" ? "claude-sonnet-4-6" : "claude-haiku-4-5-20251001",
            inputTokens      = inputTok,
            cachedReadTokens = cachedTok,
            cacheWriteTokens = 0,
            outputTokens     = outputTok,
            usdInput         = 0.001m,
            usdCachedRead    = 0.001m,
            usdCacheWrite    = 0.000m,
            usdOutput        = usdTotal - 0.002m,
            usdTotal,
            timestamp        = "2026-04-23T14:23:00Z"
        };
        File.AppendAllText(path, JsonSerializer.Serialize(entry) + "\n");
    }

    // -- Model factories -----------------------------------------------------------

    private static OpusSpecPacket MakeSpec(string specId, string missionId)
        => new()
        {
            SpecId    = specId,
            MissionId = missionId,
            Title     = $"Fixture spec {specId}",
            Inputs    = new SpecInputs { ReferenceFiles = new List<string>() },
            AcceptanceTests = new List<SpecAcceptanceTest>()
        };

    private static SonnetResult MakeSonnetResult(
        string              specId,
        string              workerId,
        OutcomeCode         outcome,
        AcceptanceTestResult[]? ats          = null,
        ScenarioBatch?          scenarioBatch = null)
        => new()
        {
            SpecId                = specId,
            WorkerId              = workerId,
            Outcome               = outcome,
            AcceptanceTestResults = ats?.ToList() ?? new List<AcceptanceTestResult>(),
            TokensUsed            = new TokenUsage { Input = 1000, CachedRead = 5000, Output = 500 },
            ScenarioBatch         = scenarioBatch
        };

    private static HaikuResult MakeHaikuResult(
        string                     scenarioId,
        string                     batchId,
        string                     workerId,
        OutcomeCode                outcome,
        Dictionary<string, double>? keyMetrics = null)
        => new()
        {
            ScenarioId       = scenarioId,
            ParentBatchId    = batchId,
            WorkerId         = workerId,
            Outcome          = outcome,
            AssertionResults = new List<AssertionResult>
            {
                new() { Id = "A-01", Passed = outcome == OutcomeCode.Ok },
                new() { Id = "A-02", Passed = outcome == OutcomeCode.Ok },
                new() { Id = "A-03", Passed = outcome == OutcomeCode.Ok }
            },
            TokensUsed = new TokenUsage { Input = 100, CachedRead = 500, Output = 50 },
            KeyMetrics = keyMetrics
        };
}
