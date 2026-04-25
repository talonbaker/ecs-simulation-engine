using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Warden.Anthropic;
using Warden.Contracts;
using Warden.Contracts.Handshake;
using Warden.Orchestrator.Batch;
using Warden.Orchestrator.Cache;
using Warden.Orchestrator.Dispatcher;
using Warden.Orchestrator.Mocks;
using Warden.Orchestrator.Persistence;
using InfraLedger = Warden.Orchestrator.Infrastructure.CostLedger;

namespace Warden.Orchestrator;

/// <summary>
/// Entry point for <c>Warden.Orchestrator resume --run-id &lt;id&gt;</c>.
/// Reloads mission and Sonnet specs from the run root, skips workers whose
/// <c>result.json</c> is present and schema-valid, and dispatches the rest.
/// </summary>
public static class ResumeCommand
{
    public static Command Build()
    {
        var runIdArg = new Argument<string>("run-id", "Run identifier to resume.");

        var budgetOpt = new Option<decimal>(
            "--budget-usd",
            () => decimal.MaxValue,
            "Hard USD ceiling for resumed dispatch. Halts when exceeded.");

        var mockOpt = new Option<bool>(
            "--mock-anthropic",
            () => false,
            "Use in-process mock instead of the real Anthropic API.");

        var mocksOpt = new Option<string?>(
            "--mocks-dir",
            () => null,
            "Directory containing canned mock responses (required when --mock-anthropic is set).");

        var runsRootOpt = new Option<string?>(
            "--runs-root",
            () => null,
            "Root directory containing run directories (default: 'runs').");

        var cmd = new Command("resume", "Resume a previously interrupted run.");
        cmd.AddArgument(runIdArg);
        cmd.AddOption(budgetOpt);
        cmd.AddOption(mockOpt);
        cmd.AddOption(mocksOpt);
        cmd.AddOption(runsRootOpt);

        cmd.SetHandler(async ctx =>
        {
            var runId    = ctx.ParseResult.GetValueForArgument(runIdArg);
            var budget   = ctx.ParseResult.GetValueForOption(budgetOpt);
            var mock     = ctx.ParseResult.GetValueForOption(mockOpt);
            var mocksDir = ctx.ParseResult.GetValueForOption(mocksOpt);
            var runsRoot = ctx.ParseResult.GetValueForOption(runsRootOpt) ?? "runs";
            var ct       = ctx.GetCancellationToken();

            ctx.ExitCode = await ResumeAsync(runId, budget, mock, mocksDir, runsRoot, ct);
        });

        return cmd;
    }

    internal static async Task<int> ResumeAsync(
        string            runId,
        decimal           budgetUsd,
        bool              mockMode,
        string?           mocksDir,
        string            runsRoot,
        CancellationToken ct)
    {
        var runRoot = RunLayout.RunRoot(runsRoot, runId);
        if (!Directory.Exists(runRoot))
        {
            Console.Error.WriteLine($"[error] Run root not found: {runRoot}");
            return 4;
        }

        var missionPath = RunLayout.MissionFile(runsRoot, runId);
        if (!File.Exists(missionPath))
        {
            Console.Error.WriteLine($"[error] mission.md not found: {missionPath}");
            return 4;
        }

        var scanner = new ResumeScanner(runsRoot);
        IReadOnlyList<ResumeScanner.WorkerState> workers;
        try
        {
            workers = await scanner.ScanSonnetWorkersAsync(runId, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"[error] Failed to scan run root: {ex.Message}");
            return 4;
        }

        var pending = workers.Where(w => !w.IsComplete).ToList();
        if (pending.Count == 0)
        {
            Console.WriteLine($"[resume] {runId}: all workers complete. Nothing to resume.");
            return 0;
        }

        Console.WriteLine($"[resume] {runId}: {pending.Count} incomplete worker(s) to dispatch.");

        string apiKey = string.Empty;
        if (!mockMode)
        {
            apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.Error.WriteLine("[error] ANTHROPIC_API_KEY is not set.");
                return 4;
            }
        }

        var ledgerPath = RunLayout.CostLedgerFile(runsRoot, runId);
        var ledger     = new InfraLedger(ledgerPath);
        var budget     = new BudgetGuard(budgetUsd);
        var cache      = new PromptCacheManager();
        var cot        = new ChainOfThoughtStore(runsRoot);

        IAnthropicClient client;
        if (mockMode)
        {
            var dir     = mocksDir ?? Path.Combine("examples", "mocks");
            var canned  = MockCannedResponses.Load(dir);
            client      = new MockAnthropic(canned);
        }
        else
        {
            client = new AnthropicClient(apiKey);
        }

        var log        = NullLoggerFactory.Instance.CreateLogger<SonnetDispatcher>();
        var retry      = RetryPolicy.Build(mockMode ? TimeSpan.Zero : null);

        // Mock path skips the banned-pattern check (no real worktree produced).
        IWorktreeDiffSource diffSource = mockMode
            ? new NullWorktreeDiffSource(cannedDiff: null)
            : new GitWorktreeDiffSource(
                NullLoggerFactory.Instance.CreateLogger<GitWorktreeDiffSource>());

        var dispatcher = new SonnetDispatcher(client, cache, cot, ledger, budget, retry, log, diffSource);
        var controller = new ConcurrencyController();

        var specs    = new List<OpusSpecPacket>(pending.Count);
        var workerIds = new List<string>(pending.Count);

        foreach (var w in pending)
        {
            var spec = JsonSerializer.Deserialize<OpusSpecPacket>(w.SpecJson, JsonOptions.Wire);
            if (spec is null)
            {
                Console.Error.WriteLine($"[error] Cannot deserialise spec for {w.WorkerId}.");
                return 4;
            }
            specs.Add(spec);
            workerIds.Add(w.WorkerId);
        }

        var work = specs.Select((spec, i) =>
            new Func<CancellationToken, Task<SonnetResult>>(c =>
                dispatcher.RunAsync(runId, workerIds[i], spec, dryRun: false, c))).ToList();

        SonnetResult[] results;
        try
        {
            results = await controller.RunAllAsync(work, new ConsoleProgress(), ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("[warn] Resume cancelled — writing partial ledger.");
            return 2;
        }

        Console.WriteLine($"[resume] {runId}: dispatch complete.");

        if (results.Any(r => r.Outcome == OutcomeCode.Blocked)) return 2;
        if (results.Any(r => r.Outcome == OutcomeCode.Failed))  return 1;
        return 0;
    }

    private sealed class ConsoleProgress : IProgress<SonnetProgress>
    {
        public void Report(SonnetProgress value)
            => Console.WriteLine($"  [{value.WorkerId}] {value.SpecId}: {value.Status}");
    }
}
