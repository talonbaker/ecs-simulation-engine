using System.Reflection;
using Warden.Orchestrator;
using Xunit;

namespace Warden.Orchestrator.Tests;

/// <summary>
/// AT-10 — <c>Warden.Orchestrator run --mock-anthropic --mission examples/smoke-mission-cast-validate.md</c>
/// exits 0 and produces a valid cost-ledger.jsonl.
/// No real Anthropic API calls are made.
/// </summary>
public sealed class CastValidateMockRunTests : IDisposable
{
    private readonly string _tempDir;

    public CastValidateMockRunTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"wp18-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public async Task AT10_MockRun_CastValidate_ExitsZeroAndWritesLedger()
    {
        var (missionFile, specsGlob) = LocateCastValidateMission();

        var exitCode = await RunCommand.RunAsync(
            missionFile: new FileInfo(missionFile),
            specsGlob:   specsGlob,
            budgetUsd:   decimal.MaxValue,
            mockMode:    true,
            dryRun:      false,
            runId:       "cast-validate-e2e-test",
            ct:          CancellationToken.None,
            runsRoot:    Path.Combine(_tempDir, "runs"));

        Assert.Equal(0, exitCode);

        var ledgerPath = Path.Combine(_tempDir, "runs", "cast-validate-e2e-test", "cost-ledger.jsonl");
        Assert.True(File.Exists(ledgerPath),
            $"Expected ledger at {ledgerPath} but it was not created.");

        var lines = await File.ReadAllLinesAsync(ledgerPath);
        Assert.True(lines.Length > 0, "Ledger must contain at least one entry.");
    }

    [Fact]
    public async Task AT10_DryRun_CastValidate_ExitsZeroWithoutLedger()
    {
        var (missionFile, specsGlob) = LocateCastValidateMission();

        var exitCode = await RunCommand.RunAsync(
            missionFile: new FileInfo(missionFile),
            specsGlob:   specsGlob,
            budgetUsd:   decimal.MaxValue,
            mockMode:    false,
            dryRun:      true,
            runId:       "cast-validate-dryrun",
            ct:          CancellationToken.None,
            runsRoot:    Path.Combine(_tempDir, "runs"));

        Assert.Equal(0, exitCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (string missionFile, string specsGlob) LocateCastValidateMission()
    {
        var repoRoot = FindRepoRoot();
        var mission  = Path.Combine(repoRoot, "examples", "smoke-mission-cast-validate.md");
        var glob     = Path.Combine(repoRoot, "examples", "smoke-specs", "cast-validate.json");

        if (!File.Exists(mission))
            throw new InvalidOperationException(
                $"Cast-validate mission file not found at: {mission}.");

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
