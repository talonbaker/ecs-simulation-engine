using Warden.Orchestrator.Infrastructure;
using Xunit;

namespace Warden.Orchestrator.Tests.Infrastructure;

public sealed class CostLedgerTests : IDisposable
{
    private readonly string _tempDir;

    public CostLedgerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"infra-ledger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    private static LedgerEntry SampleEntry() => new()
    {
        RunId            = "run-test",
        Tier             = "sonnet",
        WorkerId         = "sonnet-01",
        Model            = "claude-sonnet-4-6",
        InputTokens      = 100,
        CachedReadTokens = 0,
        CacheWriteTokens = 0,
        OutputTokens     = 50,
        UsdInput         = 0.0003m,
        UsdCachedRead    = 0.0m,
        UsdCacheWrite    = 0.0m,
        UsdOutput        = 0.00075m,
        UsdTotal         = 0.00105m,
        Timestamp        = new DateTimeOffset(2026, 4, 26, 12, 0, 0, TimeSpan.Zero)
    };

    // -- AT_CL_01: 100 parallel appends, all persisted ----------------------------

    [Fact]
    public async Task AT_CL_01_ParallelAppends_AllPersistedAndParseable()
    {
        const int count = 100;
        var ledger = new CostLedger(Path.Combine(_tempDir, "ledger.jsonl"));

        var tasks = Enumerable.Range(0, count)
            .Select(_ => ledger.AppendAsync(SampleEntry()))
            .ToArray();

        await Task.WhenAll(tasks);

        var entries = await ledger.ReadAllAsync();
        Assert.Equal(count, entries.Count);
        Assert.All(entries, e => Assert.Equal("run-test", e.RunId));
    }

    // -- AT_CL_02: Cancelled token propagates cleanly; semaphore stays usable -----

    [Fact]
    public async Task AT_CL_02_Cancellation_DuringWait_PropagatesToCallerAndLeavesLedgerUsable()
    {
        var ledger = new CostLedger(Path.Combine(_tempDir, "ledger-cancel.jsonl"));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // WaitAsync throws OperationCanceledException (or its subclass TaskCanceledException)
        // without granting entry — so Release() is never called (correct) and the semaphore
        // stays at count=1.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ledger.AppendAsync(SampleEntry(), cts.Token));

        // The semaphore must still be available; a subsequent unconditional append succeeds.
        await ledger.AppendAsync(SampleEntry(), CancellationToken.None);

        var entries = await ledger.ReadAllAsync();
        Assert.Single(entries);
    }
}
