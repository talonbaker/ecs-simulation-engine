using System.Text;
using Warden.Contracts.Handshake;
using Warden.Orchestrator.Dispatcher;
using Xunit;

namespace Warden.Orchestrator.Tests.Dispatcher;

/// <summary>
/// Unit tests for <see cref="InlineReferenceFiles.Build"/> per WP-2.0.A design notes.
/// All file I/O uses a temporary directory that is deleted on Dispose.
/// </summary>
public sealed class InlineReferenceFilesTests : IDisposable
{
    private readonly string _repoRoot;

    public InlineReferenceFilesTests()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"wp20a-inline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_repoRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_repoRoot, recursive: true); } catch { /* best effort */ }
    }

    // -- AT-01: empty referenceFiles → (null, null, null) ---------------------

    [Fact]
    public void AT01_EmptyPaths_ReturnsNoOpOutcome()
    {
        var result = InlineReferenceFiles.Build(Array.Empty<string>(), _repoRoot);

        Assert.Null(result.InlinedBlock);
        Assert.Null(result.Reason);
        Assert.Null(result.Details);
    }

    // -- AT-02: two valid files → both BEGIN/END markers present --------------

    [Fact]
    public void AT02_TwoValidFiles_BlockContainsBothInOrder()
    {
        var rel1 = WriteFile("docs/file-a.json", """{"a": 1}""");
        var rel2 = WriteFile("docs/file-b.json", """{"b": 2}""");

        var result = InlineReferenceFiles.Build([rel1, rel2], _repoRoot);

        Assert.NotNull(result.InlinedBlock);
        Assert.Null(result.Reason);
        Assert.Null(result.Details);

        var block = result.InlinedBlock;
        Assert.Contains($"--- BEGIN {rel1} ---", block);
        Assert.Contains($"--- END {rel1} ---", block);
        Assert.Contains($"--- BEGIN {rel2} ---", block);
        Assert.Contains($"--- END {rel2} ---", block);
        Assert.Contains("""{"a": 1}""", block);
        Assert.Contains("""{"b": 2}""", block);

        // File 1 must appear before file 2
        Assert.True(block.IndexOf($"--- BEGIN {rel1} ---", StringComparison.Ordinal)
                  < block.IndexOf($"--- BEGIN {rel2} ---", StringComparison.Ordinal),
            "Files should appear in input order.");

        // Block must end with the spec-packet heading ready for the dispatcher to append specJson
        Assert.Contains("## Spec packet", block);
    }

    // -- AT-03: missing file → MissingReferenceFile, details names path --------

    [Fact]
    public void AT03_MissingFile_ReturnsMissingReferenceFile()
    {
        var missingPath = "docs/does-not-exist.json";

        var result = InlineReferenceFiles.Build([missingPath], _repoRoot);

        Assert.Null(result.InlinedBlock);
        Assert.Equal(BlockReason.MissingReferenceFile, result.Reason);
        Assert.NotNull(result.Details);
        Assert.Contains(missingPath, result.Details);
    }

    // -- AT-04: single file > cap → ToolError, details name the file ----------

    [Fact]
    public void AT04_SingleFileExceedsCap_ReturnsToolError()
    {
        // Write a file that just exceeds the custom cap (101 bytes > 100-byte cap).
        var content = new string('x', 101);
        var rel     = WriteFile("big-file.json", content);

        var result = InlineReferenceFiles.Build([rel], _repoRoot, maxSingleFileBytes: 100);

        Assert.Null(result.InlinedBlock);
        Assert.Equal(BlockReason.ToolError, result.Reason);
        Assert.NotNull(result.Details);
        Assert.Contains(rel, result.Details);
    }

    // -- AT-05: aggregate > cap → ToolError ------------------------------------

    [Fact]
    public void AT05_AggregateExceedsCap_ReturnsToolError()
    {
        // Three files of 60 bytes each = 180 bytes total; cap is 100.
        var content = new string('y', 60);
        var rel1 = WriteFile("agg-a.json", content);
        var rel2 = WriteFile("agg-b.json", content);
        var rel3 = WriteFile("agg-c.json", content);

        var result = InlineReferenceFiles.Build([rel1, rel2, rel3], _repoRoot,
            maxSingleFileBytes: 200, maxAggregateBytes: 100);

        Assert.Null(result.InlinedBlock);
        Assert.Equal(BlockReason.ToolError, result.Reason);
        Assert.NotNull(result.Details);
        Assert.Contains("200KB", result.Details, StringComparison.OrdinalIgnoreCase);
    }

    // -- AT-06: path with ".." traversal → ToolError --------------------------

    [Fact]
    public void AT06_PathTraversal_ReturnsToolError()
    {
        // Build a path that resolves outside _repoRoot.
        var escapingPath = Path.Combine("..", "etc", "passwd");

        var result = InlineReferenceFiles.Build([escapingPath], _repoRoot);

        Assert.Null(result.InlinedBlock);
        Assert.Equal(BlockReason.ToolError, result.Reason);
        Assert.NotNull(result.Details);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>
    /// Writes <paramref name="content"/> to <c>_repoRoot/<paramref name="relPath"/></c>
    /// and returns <paramref name="relPath"/> so it can be passed directly to Build.
    /// </summary>
    private string WriteFile(string relPath, string content)
    {
        var fullPath = Path.Combine(_repoRoot, relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content, Encoding.UTF8);
        return relPath;
    }
}
