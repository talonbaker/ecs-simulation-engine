using System;
using System.CommandLine;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ECSCli.Ai;
using Xunit;

namespace ECSCli.Tests.AiDescribe;

/// <summary>
/// Build-time check: regenerates the engine fact sheet and asserts it matches
/// the checked-in <c>docs/engine-fact-sheet.md</c>. Fails when the engine gains
/// new systems or SimConfig keys without a corresponding fact-sheet update.
///
/// To fix a failure: run
///   dotnet run --project ECSCli -- ai describe --out docs/engine-fact-sheet.md
/// and commit the result.
/// </summary>
// Serializes with AiVerbTests: System.CommandLine beta's RootCommand singleton
// is not safe for concurrent InvokeAsync calls across xUnit test classes.
[Collection("AiCommandSingleton")]
public sealed class FactSheetStalenessTests
{
    // -------------------------------------------------------------------------
    // AT-01 / AT-03 / AT-04 — primary staleness check
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FactSheet_IsCurrent_ForCurrentEngineState()
    {
        // Arrange: locate the checked-in fact sheet.
        var repoRoot = FindRepoRoot();
        var checkedInPath = Path.Combine(repoRoot, "docs", "engine-fact-sheet.md");
        var checkedIn = await File.ReadAllTextAsync(checkedInPath);

        // Act: regenerate against current engine into a temp file.
        var tempPath = Path.GetTempFileName();
        try
        {
            int exitCode = await AiCommand.Root.InvokeAsync(
                new[] { "describe", "--out", tempPath });
            Assert.Equal(0, exitCode);
            var regenerated = await File.ReadAllTextAsync(tempPath);

            // Strip the **Generated:** timestamp line so comparison is wall-clock-independent.
            var checkedInNormalized  = StripGeneratedLine(checkedIn);
            var regeneratedNormalized = StripGeneratedLine(regenerated);

            // Assert: content is identical modulo timestamp.
            Assert.True(
                checkedInNormalized == regeneratedNormalized,
                "Engine fact sheet is out of date. " +
                "Run `dotnet run --project ECSCli -- ai describe --out docs/engine-fact-sheet.md` " +
                "and commit the result.");
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ".git")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Cannot locate repo root: no .git marker found walking up from " +
            AppContext.BaseDirectory);
    }

    private static string StripGeneratedLine(string content) =>
        Regex.Replace(content.Replace("\r\n", "\n"), @"^\*\*Generated:\*\*.*$", string.Empty,
            RegexOptions.Multiline);
}
