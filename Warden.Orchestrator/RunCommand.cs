using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Warden.Anthropic;
using Warden.Contracts;
using Warden.Contracts.Handshake;
using Warden.Contracts.SchemaValidation;
using Warden.Orchestrator.Batch;
using Warden.Orchestrator.Cache;
using Warden.Orchestrator.Dispatcher;
using Warden.Orchestrator.Mocks;
using Warden.Orchestrator.Persistence;
using InfraLedger     = Warden.Orchestrator.Infrastructure.CostLedger;
using InfraCoT        = Warden.Orchestrator.Infrastructure.ChainOfThoughtStore;

namespace Warden.Orchestrator;

/// <summary>
/// Entry point for <c>Warden.Orchestrator run</c>.
/// Implements the full mission dispatch loop described in SRD §2 Pillar B.
/// </summary>
public static class RunCommand
{
    public static Command Build()
    {
        var missionOpt = new Option<FileInfo>(
            "--mission",
            "Path to the mission brief Markdown file.") { IsRequired = true };

        var specsOpt = new Option<string>(
            "--specs",
            "Glob pattern for SpecPacket JSON files (e.g. 'specs/*.json').") { IsRequired = true };

        var budgetOpt = new Option<decimal>(
            "--budget-usd",
            () => decimal.MaxValue,
            "Hard USD ceiling for this run. Dispatch halts when exceeded.");

        var mockOpt = new Option<bool>(
            "--mock-anthropic",
            () => false,
            "Use in-process mock instead of the real Anthropic API.");

        var dryRunOpt = new Option<bool>(
            "--dry-run",
            () => false,
            "Assemble and print prompts without making any API calls.");

        var runIdOpt = new Option<string?>(
            "--run-id",
            () => null,
            "Explicit run identifier; auto-generated if omitted.");

        var cmd = new Command("run", "Execute a mission against a set of Sonnet worker specs.");
        cmd.AddOption(missionOpt);
        cmd.AddOption(specsOpt);
        cmd.AddOption(budgetOpt);
        cmd.AddOption(mockOpt);
        cmd.AddOption(dryRunOpt);
        cmd.AddOption(runIdOpt);

        cmd.SetHandler(async ctx =>
        {
            var mission = ctx.ParseResult.GetValueForOption(missionOpt)!;
            var specs   = ctx.ParseResult.GetValueForOption(specsOpt)!;
            var budget  = ctx.ParseResult.GetValueForOption(budgetOpt);
            var mock    = ctx.ParseResult.GetValueForOption(mockOpt);
            var dryRun  = ctx.ParseResult.GetValueForOption(dryRunOpt);
            var runId   = ctx.ParseResult.GetValueForOption(runIdOpt)
                          ?? GenerateRunId();
            var ct      = ctx.GetCancellationToken();

            ctx.ExitCode = await RunAsync(mission, specs, budget, mock, dryRun, runId, ct);
        });

        return cmd;
    }

    internal static async Task<int> RunAsync(
        FileInfo          missionFile,
        string            specsGlob,
        decimal           budgetUsd,
        bool              mockMode,
        bool              dryRun,
        string            runId,
        CancellationToken ct,
        string?           runsRoot = null)
    {
        // -- 1. Load mission -------------------------------------------------------
        if (!missionFile.Exists)
        {
            Console.Error.WriteLine($"[error] Mission file not found: {missionFile.FullName}");
            return 4;
        }

        // -- 2. Resolve specs ------------------------------------------------------
        var specFiles = ResolveGlob(specsGlob);
        if (specFiles.Count == 0)
        {
            Console.Error.WriteLine($"[error] No spec files matched: {specsGlob}");
            return 4;
        }

        // -- 3. Load and validate specs --------------------------------------------
        var specs = new List<OpusSpecPacket>(specFiles.Count);
        foreach (var file in specFiles)
        {
            var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            var validation = SchemaValidator.Validate(json, Schema.OpusToSonnet);
            if (!validation.IsValid)
            {
                Console.Error.WriteLine(
                    $"[error] Spec '{file}' failed schema validation: " +
                    string.Join("; ", validation.Errors));
                return 4;
            }

            var spec = JsonSerializer.Deserialize<OpusSpecPacket>(json, JsonOptions.Wire);
            if (spec is null)
            {
                Console.Error.WriteLine($"[error] Could not deserialise spec: {file}");
                return 4;
            }
            specs.Add(spec);
        }

        // -- 4. API key guard (skipped in mock/dry-run) ----------------------------
        string apiKey = string.Empty;
        if (!mockMode && !dryRun)
        {
            apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.Error.WriteLine(
                    "[error] ANTHROPIC_API_KEY environment variable is not set.");
                return 4;
            }
        }

        // -- 5. Wire up infrastructure --------------------------------------------
        var root       = runsRoot ?? "runs";
        var runDir     = Path.Combine(root, runId);
        var ledgerPath = Path.Combine(runDir, "cost-ledger.jsonl");
        Directory.CreateDirectory(runDir);

        var ledger = new InfraLedger(ledgerPath);
        var budget = new BudgetGuard(budgetUsd);
        var cache  = new PromptCacheManager();

        IAnthropicClient client;
        if (mockMode)
        {
            var mocksDir = ResolveMocksDirectory(specsGlob);
            var canned   = MockCannedResponses.Load(mocksDir);
            client       = new MockAnthropic(canned);
        }
        else
        {
            client = new AnthropicClient(apiKey);
        }

        IChainOfThoughtStore cot       = new NullChainOfThoughtStore();
        var                  log       = NullLoggerFactory.Instance.CreateLogger<SonnetDispatcher>();
        var                  retry     = RetryPolicy.Build(dryRun || mockMode ? TimeSpan.Zero : null);
        var                  dispatcher = new SonnetDispatcher(client, cache, cot, ledger, budget, retry, log);
        var                  controller = new ConcurrencyController();

        // -- 6. Dry-run shortcut ---------------------------------------------------
        if (dryRun)
        {
            Console.WriteLine($"[dry-run] {specs.Count} spec(s) — no API calls will be made.");
            foreach (var spec in specs)
                await dispatcher.RunAsync(runId, WorkerId(specs, spec), spec, dryRun: true, ct)
                    .ConfigureAwait(false);
            return 0;
        }

        // -- 7. Fan-out Sonnets ----------------------------------------------------
        Console.WriteLine($"[run] {runId}: dispatching {specs.Count} Sonnet(s)…");

        var progress = new ConsoleProgress();
        var work = specs.Select(spec =>
            new Func<CancellationToken, Task<SonnetResult>>(c =>
                dispatcher.RunAsync(runId, WorkerId(specs, spec), spec, dryRun: false, c))).ToList();

        SonnetResult[] sonnetResults;
        try
        {
            sonnetResults = await controller.RunAllAsync(work, progress, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("[warn] Run cancelled — writing partial ledger.");
            return 2;
        }

        // -- 8. Determine Sonnet exit code ----------------------------------------
        bool anyBlocked = sonnetResults.Any(r => r.Outcome == OutcomeCode.Blocked);
        bool anyFailed  = sonnetResults.Any(r => r.Outcome == OutcomeCode.Failed);

        if (budget.SpentUsd >= budgetUsd)
        {
            Console.Error.WriteLine($"[warn] Budget exhausted: ${budget.SpentUsd:F4} of ${budgetUsd:F4}.");
            return 3;
        }

        // -- 9. Collect ScenarioBatches and dispatch Haiku ------------------------
        var pendingBatches = sonnetResults
            .Where(r => r.Outcome == OutcomeCode.Ok && r.ScenarioBatch is not null)
            .Select(r => r.ScenarioBatch!)
            .ToList();

        if (pendingBatches.Count > 0)
        {
            Console.WriteLine($"[run] Dispatching {pendingBatches.Sum(b => b.Scenarios.Count)} Haiku scenario(s)…");

            var batchLog  = NullLoggerFactory.Instance.CreateLogger<BatchScheduler>();
            var cot2      = new InfraCoT(runDir);
            var scheduler = new BatchScheduler(client, cache, cot2, ledger, batchLog);
            var haiku     = new HaikuDispatcher(scheduler);

            IReadOnlyList<HaikuResult> haikuResults;
            try
            {
                haikuResults = await haiku.RunAsync(runId, pendingBatches, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("[warn] Haiku batch cancelled — writing partial ledger.");
                return 2;
            }

            if (haikuResults.Any(r => r.Outcome == OutcomeCode.Blocked)) anyBlocked = true;
            if (haikuResults.Any(r => r.Outcome == OutcomeCode.Failed))  anyFailed  = true;
        }

        // -- 10. Report stub (WP-12 fills this in) --------------------------------
        Console.WriteLine($"[run] {runId}: complete. Ledger: {ledgerPath}");

        // -- 11. Return exit code -------------------------------------------------
        if (anyBlocked) return 2;
        if (anyFailed)  return 1;
        return 0;
    }

    // -- Helpers ------------------------------------------------------------------

    private static string GenerateRunId()
    {
        var ts = DateTimeOffset.UtcNow;
        return $"{ts:yyyy-MM-ddTHHmm}-run";
    }

    private static string WorkerId(IList<OpusSpecPacket> specs, OpusSpecPacket spec)
    {
        var idx = specs.IndexOf(spec) + 1;
        return $"sonnet-{idx:D2}";
    }

    private static List<string> ResolveGlob(string glob)
    {
        var dir     = Path.GetDirectoryName(glob);
        var pattern = Path.GetFileName(glob);

        if (string.IsNullOrEmpty(dir)) dir = ".";
        if (!Directory.Exists(dir))    return new List<string>();

        return Directory
            .GetFiles(dir, pattern, SearchOption.TopDirectoryOnly)
            .OrderBy(f => f)
            .ToList();
    }

    private static string ResolveMocksDirectory(string specsGlob)
    {
        // Try "<specs-parent>/mocks" or "<specs-parent>/../mocks" or "examples/mocks"
        var specsDir = Path.GetDirectoryName(specsGlob) ?? ".";
        var candidate = Path.Combine(specsDir, "..", "mocks");
        if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);

        candidate = Path.Combine("examples", "mocks");
        if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);

        return Path.GetFullPath(Path.Combine(specsDir, "..", "mocks"));
    }

    private sealed class ConsoleProgress : IProgress<SonnetProgress>
    {
        public void Report(SonnetProgress value)
            => Console.WriteLine($"  [{value.WorkerId}] {value.SpecId}: {value.Status}");
    }
}
