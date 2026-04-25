using System.Text.Json;
using Warden.Contracts;
using Warden.Contracts.Handshake;
using Warden.Orchestrator.Persistence;
using Xunit;

namespace Warden.Orchestrator.Tests.Persistence;

/// <summary>
/// Unit tests for <see cref="ResumeScanner"/>.
/// Validates the AT-02 / AT-03 / AT-04 acceptance criteria from WP-10.
/// </summary>
public sealed class ResumeScannerTests : IDisposable
{
    private readonly string _tempDir;

    public ResumeScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"wp10-scanner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    private string Runs => Path.Combine(_tempDir, "runs");

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static string MakeSpecJson(string specId) =>
        JsonSerializer.Serialize(new OpusSpecPacket
        {
            SpecId      = specId,
            MissionId   = "mission-01",
            Title       = "Test spec",
            Rationale   = "unit test",
            Inputs      = new SpecInputs { ReferenceFiles = new List<string>() },
            Deliverables = new List<SpecDeliverable>(),
            AcceptanceTests = new List<SpecAcceptanceTest>
            {
                new() { Id = "AT-01", Assertion = "passes", Verification = VerificationKind.UnitTest }
            },
            TimeboxMinutes   = 10,
            WorkerBudgetUsd  = 0.10,
        }, JsonOptions.Wire);

    private static string MakeValidResultJson(string specId, string workerId) =>
        JsonSerializer.Serialize(new SonnetResult
        {
            SchemaVersion         = "0.1.0",
            SpecId                = specId,
            WorkerId              = workerId,
            Outcome               = OutcomeCode.Ok,
            AcceptanceTestResults = new List<AcceptanceTestResult>
            {
                new() { Id = "AT-01", Passed = true }
            },
            TokensUsed = new Warden.Contracts.Handshake.TokenUsage
            {
                Input = 100, CachedRead = 0, Output = 50
            }
        }, JsonOptions.Wire);

    private async Task SetupWorker(
        ChainOfThoughtStore store,
        string runId, string workerId, string specJson,
        string? resultJson = null)
    {
        await store.PersistSpecAsync(runId, workerId, specJson, default);
        if (resultJson is not null)
            await store.PersistResultAsync(runId, workerId, resultJson, default);
    }

    // ── AT-02 ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// AT-02: After 2 Sonnet completions, the scanner identifies only specs 3/4/5 as pending.
    /// </summary>
    [Fact]
    public async Task AT02_PartialRun_ScannerIdentifiesIncompletWorkers()
    {
        const string runId = "at02-partial";
        var store   = new ChainOfThoughtStore(Runs);
        store.InitRun(runId);

        for (int i = 1; i <= 2; i++)
        {
            var id      = $"sonnet-{i:D2}";
            var specId  = $"spec-smoke-{i:D2}";
            await SetupWorker(store, runId, id,
                specJson:   MakeSpecJson(specId),
                resultJson: MakeValidResultJson(specId, id));
        }

        for (int i = 3; i <= 5; i++)
        {
            var id     = $"sonnet-{i:D2}";
            var specId = $"spec-smoke-{i:D2}";
            await SetupWorker(store, runId, id,
                specJson:   MakeSpecJson(specId),
                resultJson: null);
        }

        var scanner = new ResumeScanner(Runs);
        var workers = await scanner.ScanSonnetWorkersAsync(runId, default);

        Assert.Equal(5, workers.Count);
        Assert.Equal(2, workers.Count(w => w.IsComplete));
        Assert.Equal(3, workers.Count(w => !w.IsComplete));

        var incomplete = workers.Where(w => !w.IsComplete).Select(w => w.WorkerId).ToList();
        Assert.Contains("sonnet-03", incomplete);
        Assert.Contains("sonnet-04", incomplete);
        Assert.Contains("sonnet-05", incomplete);
    }

    // ── AT-03 ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// AT-03: <c>resume</c> does not redispatch workers whose <c>result.json</c>
    /// is present and schema-valid.
    /// </summary>
    [Fact]
    public async Task AT03_CompleteWorkers_AreNotRedispatched()
    {
        const string runId = "at03-complete";
        var store   = new ChainOfThoughtStore(Runs);
        store.InitRun(runId);

        for (int i = 1; i <= 3; i++)
        {
            var id     = $"sonnet-{i:D2}";
            var specId = $"spec-smoke-{i:D2}";
            await SetupWorker(store, runId, id,
                specJson:   MakeSpecJson(specId),
                resultJson: MakeValidResultJson(specId, id));
        }

        var scanner = new ResumeScanner(Runs);
        var workers = await scanner.ScanSonnetWorkersAsync(runId, default);

        Assert.Equal(3, workers.Count);
        Assert.All(workers, w => Assert.True(w.IsComplete,
            $"Worker {w.WorkerId} should be complete but IsComplete=false"));
    }

    // ── AT-04 ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// AT-04: <c>resume</c> with an invalid <c>result.json</c> (schema mismatch)
    /// redispatches that worker.
    /// </summary>
    [Fact]
    public async Task AT04_InvalidResultJson_WorkerIsRedispatched()
    {
        const string runId    = "at04-invalid";
        const string workerId = "sonnet-01";
        const string specId   = "spec-smoke-01";
        var store = new ChainOfThoughtStore(Runs);
        store.InitRun(runId);

        await SetupWorker(store, runId, workerId,
            specJson:   MakeSpecJson(specId),
            resultJson: "{\"this\":\"does-not-match-the-schema\"}");

        var scanner = new ResumeScanner(Runs);
        var workers = await scanner.ScanSonnetWorkersAsync(runId, default);

        var worker = Assert.Single(workers);
        Assert.False(worker.IsComplete,
            "Worker with schema-invalid result.json should be marked for redispatch.");
        Assert.Equal(workerId, worker.WorkerId);
        Assert.Null(worker.ResultJson);
    }

    /// <summary>
    /// AT-04 (variant): A completely empty <c>result.json</c> is also treated as invalid.
    /// </summary>
    [Fact]
    public async Task AT04_EmptyResultJson_WorkerIsRedispatched()
    {
        const string runId    = "at04-empty";
        const string workerId = "sonnet-01";
        const string specId   = "spec-smoke-01";
        var store = new ChainOfThoughtStore(Runs);
        store.InitRun(runId);

        await SetupWorker(store, runId, workerId,
            specJson:   MakeSpecJson(specId),
            resultJson: "");

        var scanner = new ResumeScanner(Runs);
        var workers = await scanner.ScanSonnetWorkersAsync(runId, default);

        var worker = Assert.Single(workers);
        Assert.False(worker.IsComplete);
    }

    // ── Edge cases ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunRootNotFound_ThrowsDirectoryNotFoundException()
    {
        var scanner = new ResumeScanner(Runs);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => scanner.ScanSonnetWorkersAsync("nonexistent-run", default));
    }

    [Fact]
    public async Task EmptyRunRoot_ReturnsEmptyList()
    {
        const string runId = "at-empty-run";
        var store = new ChainOfThoughtStore(Runs);
        store.InitRun(runId);

        var scanner = new ResumeScanner(Runs);
        var workers = await scanner.ScanSonnetWorkersAsync(runId, default);

        Assert.Empty(workers);
    }
}
