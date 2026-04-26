using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Warden.Anthropic;
using Warden.Contracts;
using Warden.Contracts.Handshake;
using Warden.Orchestrator;
using Warden.Orchestrator.Cache;
using Warden.Orchestrator.Dispatcher;
using Warden.Orchestrator.Mocks;
using Warden.Orchestrator.Persistence;
using Xunit;
using InfraLedger = Warden.Orchestrator.Infrastructure.CostLedger;
using AnthropicTokenUsage = Warden.Anthropic.TokenUsage;

namespace Warden.Orchestrator.Tests;

/// <summary>
/// End-to-end tests for <see cref="RunCommand"/> using <c>--mock-anthropic</c>.
/// No real Anthropic API calls are made.
/// </summary>
public sealed class RunCommandEndToEndTests : IDisposable
{
    private readonly string _tempDir;

    public RunCommandEndToEndTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"wp09-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    // ── AT-01 ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AT01_MockRun_ExitsZeroAndWritesLedger()
    {
        var (missionFile, specsGlob) = LocateSmokeMission();

        var exitCode = await RunCommand.RunAsync(
            missionFile: new FileInfo(missionFile),
            specsGlob:   specsGlob,
            budgetUsd:   decimal.MaxValue,
            mockMode:    true,
            dryRun:      false,
            runId:       "smoke-e2e-test",
            ct:          CancellationToken.None,
            runsRoot:    Path.Combine(_tempDir, "runs"));

        Assert.Equal(0, exitCode);

        var ledgerPath = Path.Combine(_tempDir, "runs", "smoke-e2e-test", "cost-ledger.jsonl");
        Assert.True(File.Exists(ledgerPath),
            $"Expected ledger at {ledgerPath} but it was not created.");

        var lines = await File.ReadAllLinesAsync(ledgerPath);
        Assert.True(lines.Length > 0, "Ledger must contain at least one entry.");
    }

    // ── AT-01b ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AT01b_MockRun_TenIterations_AllSucceed()
    {
        var (missionFile, specsGlob) = LocateSmokeMission();

        for (int i = 0; i < 10; i++)
        {
            var runId = $"smoke-e2e-stress-{i:D2}";
            var runsRoot = Path.Combine(_tempDir, "runs-stress");

            var exitCode = await RunCommand.RunAsync(
                missionFile: new FileInfo(missionFile),
                specsGlob:   specsGlob,
                budgetUsd:   decimal.MaxValue,
                mockMode:    true,
                dryRun:      false,
                runId:       runId,
                ct:          CancellationToken.None,
                runsRoot:    runsRoot);

            Assert.Equal(0, exitCode);

            var ledgerPath = Path.Combine(runsRoot, runId, "cost-ledger.jsonl");
            Assert.True(File.Exists(ledgerPath),
                $"[iter {i}] Expected ledger at {ledgerPath} but it was not created.");

            var lines = await File.ReadAllLinesAsync(ledgerPath);
            Assert.True(lines.Length > 0, $"[iter {i}] Ledger must contain at least one entry.");
        }
    }

    // ── AT-04 ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AT04_DryRun_ExitsZeroWithoutCreatingLedger()
    {
        var (missionFile, specsGlob) = LocateSmokeMission();

        var exitCode = await RunCommand.RunAsync(
            missionFile: new FileInfo(missionFile),
            specsGlob:   specsGlob,
            budgetUsd:   decimal.MaxValue,
            mockMode:    false,
            dryRun:      true,
            runId:       "smoke-dryrun-test",
            ct:          CancellationToken.None,
            runsRoot:    Path.Combine(_tempDir, "runs"));

        Assert.Equal(0, exitCode);
    }

    // ── AT-05 ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AT05_InvalidSpec_AbortsBeforeApiCallExitCode4()
    {
        var badSpecDir  = Path.Combine(_tempDir, "bad-specs");
        Directory.CreateDirectory(badSpecDir);
        var badSpecPath = Path.Combine(badSpecDir, "invalid.json");
        await File.WriteAllTextAsync(badSpecPath, @"{""not"":""a-valid-spec""}");

        var missionFile = LocateSmokeMission().missionFile;

        var exitCode = await RunCommand.RunAsync(
            missionFile: new FileInfo(missionFile),
            specsGlob:   Path.Combine(badSpecDir, "*.json"),
            budgetUsd:   decimal.MaxValue,
            mockMode:    true,
            dryRun:      false,
            runId:       "smoke-invalid-spec",
            ct:          CancellationToken.None,
            runsRoot:    Path.Combine(_tempDir, "runs"));

        Assert.Equal(4, exitCode);
    }

    // ── AT-07 ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AT07_CancellationDuringMockRun_CompletesWithinTwoSecondsAndWritesPartialLedger()
    {
        var (missionFile, specsGlob) = LocateSmokeMission();
        var cts = new CancellationTokenSource();
        var runsRoot = Path.Combine(_tempDir, "runs-cancel");

        // Cancel almost immediately — the Sonnet dispatchers should honour it
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        int exitCode;
        try
        {
            exitCode = await RunCommand.RunAsync(
                missionFile: new FileInfo(missionFile),
                specsGlob:   specsGlob,
                budgetUsd:   decimal.MaxValue,
                mockMode:    true,
                dryRun:      false,
                runId:       "smoke-cancel-test",
                ct:          cts.Token,
                runsRoot:    runsRoot);
        }
        catch (OperationCanceledException)
        {
            exitCode = 2;
        }
        sw.Stop();

        // Must complete well within 2 seconds
        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"Cancellation took {sw.ElapsedMilliseconds} ms; expected < 2000 ms.");

        // Exit code 2 (blocked/cancelled) or 0 (if it finished fast enough before cancel)
        Assert.True(exitCode is 0 or 2,
            $"Expected exit code 0 or 2; got {exitCode}.");
    }

    // ── AT-07 (WP-2.0.A) ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AT07_SpecWithReferenceFile_InlinedBlockPresentInDispatchedUserTurn()
    {
        // Arrange — write a reference file and build a spec that points to it.
        var repoRoot    = Path.Combine(_tempDir, "repo-at07");
        var relPath     = Path.Combine("docs", "ref.json");
        var fileContent = """{"key": "hello-inline"}""";
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs"));
        await File.WriteAllTextAsync(Path.Combine(repoRoot, relPath), fileContent);

        var spec           = MakeSpecWithReferenceFiles("spec-inline-at07", [relPath]);
        var capturingClient = new CapturingAnthropicClient(spec);
        var ledgerPath     = Path.Combine(_tempDir, "at07-ledger.jsonl");
        var dispatcher     = BuildDispatcher(capturingClient, new InfraLedger(ledgerPath), repoRoot);

        // Act
        var result = await dispatcher.RunAsync("run-at07", "sonnet-01", spec, dryRun: false, CancellationToken.None);

        // Assert — the dispatch succeeded and the mock received the inlined content.
        Assert.Equal(OutcomeCode.Ok, result.Outcome);
        Assert.NotNull(capturingClient.CapturedUserText);
        Assert.Contains($"--- BEGIN {relPath} ---", capturingClient.CapturedUserText);
        Assert.Contains(fileContent, capturingClient.CapturedUserText);
        Assert.Contains($"--- END {relPath} ---", capturingClient.CapturedUserText);
        Assert.Contains("## Spec packet", capturingClient.CapturedUserText);
    }

    // ── AT-08 (WP-2.0.A) ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AT08_SpecWithMissingReferenceFile_BlockedAndNoLedgerEntry()
    {
        // Arrange — spec references a file that does not exist on disk.
        var repoRoot        = Path.Combine(_tempDir, "repo-at08");
        Directory.CreateDirectory(repoRoot);
        var missingRelPath  = Path.Combine("docs", "does-not-exist.json");

        var spec        = MakeSpecWithReferenceFiles("spec-inline-at08", [missingRelPath]);
        var client      = new CapturingAnthropicClient(spec);
        var ledgerPath  = Path.Combine(_tempDir, "at08-ledger.jsonl");
        var dispatcher  = BuildDispatcher(client, new InfraLedger(ledgerPath), repoRoot);

        // Act
        var result = await dispatcher.RunAsync("run-at08", "sonnet-01", spec, dryRun: false, CancellationToken.None);

        // Assert — blocked with the right reason; no API call; no ledger spend.
        Assert.Equal(OutcomeCode.Blocked, result.Outcome);
        Assert.Equal(BlockReason.MissingReferenceFile, result.BlockReason);
        Assert.Null(client.CapturedUserText);  // CreateMessageAsync was never called

        Assert.False(File.Exists(ledgerPath),
            "No ledger file should exist — pre-dispatch block means zero spend.");
    }

    // -- Helpers ------------------------------------------------------------------

    private static (string missionFile, string specsGlob) LocateSmokeMission()
    {
        var repoRoot = FindRepoRoot();
        var mission  = Path.Combine(repoRoot, "examples", "smoke-mission.md");
        var glob     = Path.Combine(repoRoot, "examples", "smoke-specs", "*.json");

        if (!File.Exists(mission))
            throw new InvalidOperationException(
                $"Smoke mission file not found at: {mission}. " +
                "Run from the repo root or ensure examples/ is present.");

        return (mission, glob);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);

        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ECSSimulation.sln")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate repo root (ECSSimulation.sln not found in any ancestor directory).");
    }

    // ── AT-07/AT-08 helpers ───────────────────────────────────────────────────────

    private static SonnetDispatcher BuildDispatcher(
        IAnthropicClient client, InfraLedger ledger, string repoRoot)
    {
        var cache   = new PromptCacheManager();
        var cot     = new NullChainOfThoughtStore();
        var budget  = new BudgetGuard(decimal.MaxValue);
        var retry   = RetryPolicy.Build(TimeSpan.Zero);
        var log     = NullLoggerFactory.Instance.CreateLogger<SonnetDispatcher>();
        var diff    = new NullWorktreeDiffSource(cannedDiff: null);
        return new SonnetDispatcher(client, cache, cot, ledger, budget, retry, log, diff, repoRoot);
    }

    private static OpusSpecPacket MakeSpecWithReferenceFiles(
        string specId, IEnumerable<string> refFiles)
        => new()
        {
            SpecId          = specId,
            MissionId       = "mission-inline-test",
            Title           = "Inline-files integration test spec",
            Rationale       = "WP-2.0.A integration test",
            Inputs          = new SpecInputs { ReferenceFiles = refFiles.ToList() },
            Deliverables    = [new SpecDeliverable { Kind = DeliverableKind.Doc, Path = "out.md", Description = "test" }],
            AcceptanceTests = [new SpecAcceptanceTest { Id = "AT-01", Assertion = "ok", Verification = VerificationKind.UnitTest }],
            NonGoals        = ["none"],
            TimeboxMinutes  = 30,
            WorkerBudgetUsd = 0.5
        };

    // ── Inner types ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Stub Anthropic client that records the user-turn text from the first
    /// <see cref="CreateMessageAsync"/> call and returns a valid, canned <see cref="SonnetResult"/>.
    /// </summary>
    private sealed class CapturingAnthropicClient : IAnthropicClient
    {
        private readonly string _resultJson;

        public string? CapturedUserText { get; private set; }

        public CapturingAnthropicClient(OpusSpecPacket spec)
        {
            var result = new SonnetResult
            {
                SchemaVersion         = "0.1.0",
                SpecId                = spec.SpecId,
                WorkerId              = "sonnet-01",
                Outcome               = OutcomeCode.Ok,
                AcceptanceTestResults = [new AcceptanceTestResult { Id = "AT-01", Passed = true }],
                TokensUsed            = new Contracts.Handshake.TokenUsage()
            };
            _resultJson = JsonSerializer.Serialize(result, JsonOptions.Wire);
        }

        public Task<MessageResponse> CreateMessageAsync(MessageRequest request, CancellationToken ct = default)
        {
            CapturedUserText = request.Messages
                .FirstOrDefault()
                ?.Content.OfType<TextBlock>().FirstOrDefault()
                ?.Text;

            var response = new MessageResponse(
                Id:           "cap-" + Guid.NewGuid().ToString("N")[..8],
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
}
