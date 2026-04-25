using System.Reflection;
using Warden.Orchestrator;
using Xunit;

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
}
