using System.Text.Json;
using Warden.Anthropic;
using Warden.Orchestrator.Persistence;
using Xunit;

namespace Warden.Orchestrator.Tests.Persistence;

public class CostLedgerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LedgerEntry SampleEntry(string runId = "run-test") => new(
        RunId:            runId,
        Tier:             "sonnet",
        WorkerId:         "sonnet-01",
        Model:            ModelId.SonnetV46,
        InputTokens:      1240,
        CachedReadTokens: 32180,
        CacheWriteTokens: 0,
        OutputTokens:     720,
        UsdInput:         0.00372m,
        UsdCachedRead:    0.009654m,
        UsdCacheWrite:    0.0m,
        UsdOutput:        0.01080m,
        UsdTotal:         0.024174m,
        Timestamp:        new DateTimeOffset(2026, 4, 23, 14, 23, 7, TimeSpan.Zero));

    // ── AT-01: CostCalculator produces the exact §3 figure ───────────────────

    /// <summary>
    /// AT-01: Sonnet call — 32 k cached-read + 2.5 k input + 1.5 k output (non-batch).
    /// Expected total from 02-cost-model.md §3 rates:
    ///   32 000 / 1 M × $0.30  =  $0.0096
    ///  + 2 500 / 1 M × $3.00  =  $0.0075
    ///  + 1 500 / 1 M × $15.00 =  $0.0225
    ///                           ────────
    ///                            $0.0396
    /// </summary>
    [Fact]
    public void AT01_CostCalculator_SonnetCachedCall_MatchesCostModelSection3()
    {
        var calc = new CostCalculator();
        var usage = new TokenUsage(
            InputTokens:                2_500,
            CacheCreationInputTokens:   0,
            CacheReadInputTokens:       32_000,
            OutputTokens:               1_500);

        var result = calc.CalculateUsd(ModelId.SonnetV46, usage, isBatch: false);

        Assert.Equal(0.0396m, result);
    }

    // ── AT-02: JSONL round-trip ───────────────────────────────────────────────

    [Fact]
    public async Task AT02_CostLedger_WritesOneJsonlLinePerEntry_RoundTripsCleanly()
    {
        var path = Path.GetTempFileName();
        try
        {
            var entry = SampleEntry();

            using (var ledger = CostLedger.Open(path))
                await ledger.WriteAsync(entry);

            var lines = File.ReadAllLines(path);
            Assert.Single(lines);

            var restored = JsonSerializer.Deserialize<LedgerEntry>(lines[0]);
            Assert.NotNull(restored);
            Assert.Equal(entry.RunId,   restored!.RunId);
            Assert.Equal(entry.UsdTotal, restored.UsdTotal);
            Assert.Equal(entry.Model.Name, restored.Model.Name);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ── AT-03: Concurrent writers — no interleaved lines ─────────────────────

    [Fact]
    public async Task AT03_ConcurrentWriters_NoInterleavedLines()
    {
        const int tasks      = 10;
        const int perTask    = 100;
        const int totalLines = tasks * perTask;

        var path = Path.GetTempFileName();
        try
        {
            using (var ledger = CostLedger.Open(path))
            {
                var work = Enumerable.Range(0, tasks)
                    .Select(_ => Task.Run(async () =>
                    {
                        var entry = SampleEntry();
                        for (int i = 0; i < perTask; i++)
                            await ledger.WriteAsync(entry);
                    }))
                    .ToArray();

                await Task.WhenAll(work);
            }

            var lines = File.ReadAllLines(path);
            Assert.Equal(totalLines, lines.Length);

            foreach (var line in lines)
            {
                var entry = JsonSerializer.Deserialize<LedgerEntry>(line);
                Assert.NotNull(entry);
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ── AT-06: Partial last line leaves prior lines intact ───────────────────

    /// <summary>
    /// AT-06: Simulates a crash mid-write by appending an incomplete JSON fragment
    /// after N complete, fsynced entries.  File.ReadAllLines must still produce
    /// N parseable lines; the partial line is unparseable but does not corrupt
    /// the preceding content.
    /// </summary>
    [Fact]
    public async Task AT06_CrashMidWrite_PriorLinesRemainingParseable()
    {
        const int completeEntries = 5;
        var path = Path.GetTempFileName();
        try
        {
            // Write 5 complete, fsynced entries.
            using (var ledger = CostLedger.Open(path))
            {
                for (int i = 0; i < completeEntries; i++)
                    await ledger.WriteAsync(SampleEntry($"run-{i}"));
            }

            // Simulate crash mid-write: append a truncated JSON object.
            // This is what happens when the process dies after WriteAsync has
            // started but before Flush(true) completes.
            await File.AppendAllTextAsync(path, "{\"runId\":\"crash-partial\"");

            var lines = File.ReadAllLines(path);
            Assert.Equal(completeEntries + 1, lines.Length);

            int parsed = 0;
            foreach (var line in lines)
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<LedgerEntry>(line);
                    if (entry is not null) parsed++;
                }
                catch (JsonException) { } // expected for the partial line
            }
            Assert.Equal(completeEntries, parsed);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ── AT-07: Zero tokens → decimal.Zero ────────────────────────────────────

    [Fact]
    public void AT07_CostCalculator_ZeroTokens_ReturnsDecimalZero()
    {
        var calc  = new CostCalculator();
        var usage = new TokenUsage(0, 0, 0, 0);

        Assert.Equal(decimal.Zero, calc.CalculateUsd(ModelId.SonnetV46, usage, isBatch: false));
        Assert.Equal(decimal.Zero, calc.CalculateUsd(ModelId.HaikuV45,  usage, isBatch: true));
        Assert.Equal(decimal.Zero, calc.CalculateUsd(ModelId.OpusV46,   usage, isBatch: false));
    }
}
