using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Warden.Anthropic;
using Warden.Contracts;
using Warden.Contracts.Handshake;
using Warden.Orchestrator.Cache;
using Warden.Orchestrator.Dispatcher;
using InfraLedger = Warden.Orchestrator.Infrastructure.CostLedger;
using Warden.Orchestrator.Mocks;
using Warden.Orchestrator.Persistence;
using Xunit;
using AnthropicTokenUsage = Warden.Anthropic.TokenUsage;

namespace Warden.Orchestrator.Tests.Dispatcher;

/// <summary>
/// Acceptance tests for WP-13: banned-pattern detection wired into SonnetDispatcher.
/// </summary>
public sealed class SonnetDispatcherBannedPatternTests : IDisposable
{
    private readonly string _tempDir;

    public SonnetDispatcherBannedPatternTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"wp13-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    // ── AT-01: ok + clean diff → result unchanged ─────────────────────────────

    [Fact]
    public async Task AT01_OkResult_CleanDiff_ReturnedUnchanged()
    {
        var okResult = MakeOkResult();
        var result   = await RunDispatcher(okResult, new NullWorktreeDiffSource(MakeCleanDiff()));

        Assert.Equal(OutcomeCode.Ok, result.Outcome);
        Assert.Null(result.BlockReason);
    }

    // ── AT-02: ok + banned diff → overridden to blocked/tool-error ────────────

    [Fact]
    public async Task AT02_OkResult_BannedDiff_OverriddenToBlocked()
    {
        var okResult = MakeOkResult();
        var result   = await RunDispatcher(okResult, new NullWorktreeDiffSource(MakeBannedDiff()));

        Assert.Equal(OutcomeCode.Blocked, result.Outcome);
        Assert.Equal(BlockReason.ToolError, result.BlockReason);
        Assert.False(string.IsNullOrWhiteSpace(result.BlockDetails));
        Assert.Contains("banned pattern", result.BlockDetails, StringComparison.OrdinalIgnoreCase);
    }

    // ── AT-03: already non-ok result is never escalated ───────────────────────

    [Fact]
    public async Task AT03_BlockedResult_BannedDiff_NotEscalated()
    {
        var blocked = MakeBlockedResult(BlockReason.BuildFailed, "build broke");
        var result  = await RunDispatcher(blocked, new NullWorktreeDiffSource(MakeBannedDiff()));

        Assert.Equal(OutcomeCode.Blocked, result.Outcome);
        Assert.Equal(BlockReason.BuildFailed, result.BlockReason);
    }

    [Fact]
    public async Task AT03_FailedResult_BannedDiff_NotEscalated()
    {
        var failed = MakeFailedResult();
        var result = await RunDispatcher(failed, new NullWorktreeDiffSource(MakeBannedDiff()));

        Assert.Equal(OutcomeCode.Failed, result.Outcome);
    }

    // ── AT-04: GitWorktreeDiffSource with non-existent path → null + Info log ─

    [Fact]
    public async Task AT04_GitWorktreeDiffSource_NonExistentPath_ReturnsNullAndLogsInformation()
    {
        var logger         = new CapturingLogger<GitWorktreeDiffSource>();
        var diffSource     = new GitWorktreeDiffSource(logger);
        var nonExistentPath = Path.Combine(_tempDir, "does-not-exist");

        var diff = await diffSource.GetDiffAsync(nonExistentPath, CancellationToken.None);

        Assert.Null(diff);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information);
    }

    // ── AT-05: GitWorktreeDiffSource timeout → null + Warning log ─────────────

    [Fact]
    public async Task AT05_GitWorktreeDiffSource_Timeout_ReturnsNullAndLogsWarning()
    {
        var repoRoot = FindRepoRoot();
        var logger   = new CapturingLogger<GitWorktreeDiffSource>();

        // 1 ms timeout guarantees the git subprocess does not finish in time.
        var diffSource = new GitWorktreeDiffSource(logger, timeout: TimeSpan.FromMilliseconds(1));

        var diff = await diffSource.GetDiffAsync(repoRoot, CancellationToken.None);

        Assert.Null(diff);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    // ── AT-06: NullWorktreeDiffSource(null) skips banned-pattern check ─────────

    [Fact]
    public async Task AT06_NullDiffSource_SkipsBannedPatternCheck()
    {
        // Even with a result that has outcome=ok, null diff means no override.
        var okResult = MakeOkResult();
        var result   = await RunDispatcher(okResult, new NullWorktreeDiffSource(cannedDiff: null));

        Assert.Equal(OutcomeCode.Ok, result.Outcome);
    }

    // ── AT-07: ledger entry written before banned-pattern override ─────────────

    [Fact]
    public async Task AT07_LedgerWrittenBeforeBannedPatternOverride()
    {
        var okResult    = MakeOkResult();
        var ledgerPath  = Path.Combine(_tempDir, "at07-ledger.jsonl");
        var (dispatcher, ledger, _) = BuildDispatcher(
            okResult,
            new NullWorktreeDiffSource(MakeBannedDiff()),
            ledgerPath);

        var result = await dispatcher.RunAsync("run-at07", "sonnet-01", MakeSpec(), dryRun: false, CancellationToken.None);

        // Override was applied.
        Assert.Equal(OutcomeCode.Blocked, result.Outcome);

        // Ledger entry still exists — the spend was recorded despite the override.
        Assert.True(File.Exists(ledgerPath), "Ledger file must exist after the dispatcher returns.");
        var entries = await ledger.ReadAllAsync();
        Assert.True(entries.Count > 0, "Ledger must contain at least one entry.");
    }

    // ── AT-08: existing RunCommandEndToEndTests still pass ────────────────────
    //
    // Verified by the test runner — this class does not re-run those tests.
    // The RunCommandEndToEndTests.AT01_MockRun_ExitsZeroAndWritesLedger test
    // exercises the full RunCommand path (which now wires NullWorktreeDiffSource
    // for --mock-anthropic).  That test passing is AT-08.

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<SonnetResult> RunDispatcher(SonnetResult mockResult, IWorktreeDiffSource diffSource)
    {
        var (dispatcher, _, _) = BuildDispatcher(mockResult, diffSource,
            Path.Combine(_tempDir, $"ledger-{Guid.NewGuid():N}.jsonl"));
        return await dispatcher.RunAsync("test-run", "sonnet-01", MakeSpec(), dryRun: false, CancellationToken.None);
    }

    private static (SonnetDispatcher dispatcher, InfraLedger ledger, string tempDir)
        BuildDispatcher(SonnetResult mockResult, IWorktreeDiffSource diffSource, string ledgerPath)
    {
        var client = new StubAnthropicClient(mockResult);
        var cache  = new PromptCacheManager();
        var cot    = new NullChainOfThoughtStore();
        var ledger = new InfraLedger(ledgerPath);
        var budget = new BudgetGuard(decimal.MaxValue);
        var retry  = RetryPolicy.Build(TimeSpan.Zero);
        var log    = NullLoggerFactory.Instance.CreateLogger<SonnetDispatcher>();

        var dispatcher = new SonnetDispatcher(client, cache, cot, ledger, budget, retry, log, diffSource);
        return (dispatcher, ledger, Path.GetDirectoryName(ledgerPath)!);
    }

    private static SonnetResult MakeOkResult()
        => new()
        {
            SchemaVersion         = "0.1.0",
            SpecId                = "spec-01",
            WorkerId              = "sonnet-01",
            Outcome               = OutcomeCode.Ok,
            AcceptanceTestResults = [new AcceptanceTestResult { Id = "AT-01", Passed = true }],
            TokensUsed            = new Contracts.Handshake.TokenUsage()
        };

    private static SonnetResult MakeBlockedResult(BlockReason reason, string details)
        => new()
        {
            SchemaVersion         = "0.1.0",
            SpecId                = "spec-01",
            WorkerId              = "sonnet-01",
            Outcome               = OutcomeCode.Blocked,
            BlockReason           = reason,
            BlockDetails          = details,
            AcceptanceTestResults = [new AcceptanceTestResult { Id = "AT-01", Passed = false }],
            TokensUsed            = new Contracts.Handshake.TokenUsage()
        };

    private static SonnetResult MakeFailedResult()
        => new()
        {
            SchemaVersion         = "0.1.0",
            SpecId                = "spec-01",
            WorkerId              = "sonnet-01",
            Outcome               = OutcomeCode.Failed,
            AcceptanceTestResults = [new AcceptanceTestResult { Id = "AT-01", Passed = false }],
            TokensUsed            = new Contracts.Handshake.TokenUsage()
        };

    private static OpusSpecPacket MakeSpec()
        => new()
        {
            SpecId         = "spec-01",
            MissionId      = "mission-01",
            Title          = "Test spec",
            Rationale      = "WP-13 test",
            Inputs         = new SpecInputs { ReferenceFiles = [] },
            Deliverables   = [new SpecDeliverable { Kind = DeliverableKind.Code, Path = "x.cs", Description = "d" }],
            AcceptanceTests = [new SpecAcceptanceTest { Id = "AT-01", Assertion = "a", Verification = VerificationKind.UnitTest }],
            TimeboxMinutes = 30,
            WorkerBudgetUsd = 0.5
        };

    private static string MakeBannedDiff()
        => """
           diff --git a/Some.Project/Foo.cs b/Some.Project/Foo.cs
           --- a/Some.Project/Foo.cs
           +++ b/Some.Project/Foo.cs
           @@ -1,1 +1,2 @@
            existing line
           +Process.Start("cmd.exe", "/c dir");
           """;

    private static string MakeCleanDiff()
        => """
           diff --git a/Warden.Contracts/Foo.cs b/Warden.Contracts/Foo.cs
           --- a/Warden.Contracts/Foo.cs
           +++ b/Warden.Contracts/Foo.cs
           @@ -1,1 +1,2 @@
            existing line
           +public int Answer => 42;
           """;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ECSSimulation.sln")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate repo root (ECSSimulation.sln not found).");
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private sealed class StubAnthropicClient : IAnthropicClient
    {
        private readonly string _resultJson;

        public StubAnthropicClient(SonnetResult result)
        {
            _resultJson = JsonSerializer.Serialize(result, JsonOptions.Wire);
        }

        public Task<MessageResponse> CreateMessageAsync(MessageRequest request, CancellationToken ct = default)
        {
            var response = new MessageResponse(
                Id:           "stub-" + Guid.NewGuid().ToString("N")[..8],
                Type:         "message",
                Role:         "assistant",
                Content:      [new TextBlock(_resultJson)],
                Model:        ModelId.SonnetV46,
                StopReason:   "end_turn",
                StopSequence: null,
                Usage:        new AnthropicTokenUsage(10, 0, 0, 5));
            return Task.FromResult(response);
        }

        public Task<BatchSubmission> CreateBatchAsync(BatchRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<BatchStatus> GetBatchAsync(string batchId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<BatchResultEntry> StreamBatchResultsAsync(string batchId, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        bool ILogger.IsEnabled(LogLevel logLevel) => true;

        void ILogger.Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
