using System.Text;
using Warden.Contracts.Handshake;

namespace Warden.Orchestrator.Dispatcher;

/// <summary>
/// Pre-dispatch helper that reads repo-relative reference files and renders them
/// as a single delimited text block to prepend to the Sonnet's user turn.
///
/// Sonnets dispatched via the Anthropic API have no file-system access.  Any spec
/// whose <c>inputs.referenceFiles[]</c> lists real paths would always block with
/// <c>missing-reference-file</c> unless the orchestrator pre-reads and inlines those
/// files.  This class implements that inline mode (SRD §8.1, PHASE-1-HANDOFF §4).
/// </summary>
public static class InlineReferenceFiles
{
    /// <summary>
    /// Result of a <see cref="Build"/> call.
    /// On success: <c>InlinedBlock</c> is the formatted prefix, <c>Reason</c> and
    /// <c>Details</c> are null.  On failure: <c>InlinedBlock</c> is null, the
    /// <c>Reason</c>/<c>Details</c> pair describes what went wrong.
    /// When <c>paths</c> is empty all three are null — no preprocessing needed.
    /// </summary>
    public sealed record Outcome(string? InlinedBlock, BlockReason? Reason, string? Details);

    /// <summary>
    /// Reads each file in <paramref name="paths"/>, enforces size caps, and builds
    /// the formatted inline block.  Returns the block on success, or a structured
    /// failure reason on any error.
    /// </summary>
    /// <param name="paths">Repo-relative paths from <c>spec.Inputs.ReferenceFiles</c>.</param>
    /// <param name="repoRoot">Absolute path to the repository root used to resolve relative paths.</param>
    /// <param name="maxSingleFileBytes">Per-file size cap in bytes of file contents.</param>
    /// <param name="maxAggregateBytes">Aggregate size cap across all files.</param>
    public static Outcome Build(
        IReadOnlyList<string> paths,
        string                repoRoot,
        int                   maxSingleFileBytes = 100_000,
        int                   maxAggregateBytes  = 200_000)
    {
        if (paths.Count == 0)
            return new Outcome(null, null, null);

        // Normalise the repo root once; always ends with a separator so prefix-check is unambiguous.
        var normalRoot = Path.GetFullPath(repoRoot);
        if (!normalRoot.EndsWith(Path.DirectorySeparatorChar))
            normalRoot += Path.DirectorySeparatorChar;

        var sb = new StringBuilder();
        sb.AppendLine("## Inlined reference files");
        sb.AppendLine();
        sb.AppendLine("The following files are provided inline because Sonnets dispatched through the");
        sb.AppendLine("Anthropic API do not have file-system access. Each file appears between");
        sb.AppendLine("`--- BEGIN <path> ---` and `--- END <path> ---` markers. Treat the contents");
        sb.AppendLine("as authoritative; do not request file access.");
        sb.AppendLine();

        long aggregateBytes = 0;

        foreach (var relPath in paths)
        {
            // Resolve the path and reject any escape via ".." sequences.
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(Path.Combine(repoRoot, relPath));
            }
            catch (Exception)
            {
                return new Outcome(null, BlockReason.ToolError,
                    $"reference file path is invalid: {relPath}");
            }

            if (!fullPath.StartsWith(normalRoot, StringComparison.OrdinalIgnoreCase))
                return new Outcome(null, BlockReason.ToolError,
                    $"reference file path escapes repository root: {relPath}");

            if (!File.Exists(fullPath))
                return new Outcome(null, BlockReason.MissingReferenceFile,
                    $"reference file not found: {relPath}");

            long singleBytes = new FileInfo(fullPath).Length;

            if (singleBytes > maxSingleFileBytes)
                return new Outcome(null, BlockReason.ToolError,
                    $"reference file {relPath} exceeds 100KB ({singleBytes} bytes)");

            aggregateBytes += singleBytes;
            if (aggregateBytes > maxAggregateBytes)
                return new Outcome(null, BlockReason.ToolError,
                    $"aggregate reference files exceed 200KB ({aggregateBytes} bytes)");

            var contents = File.ReadAllText(fullPath, Encoding.UTF8);
            sb.AppendLine($"--- BEGIN {relPath} ---");
            sb.Append(contents);
            if (contents.Length == 0 || contents[^1] != '\n')
                sb.AppendLine();
            sb.AppendLine($"--- END {relPath} ---");
            sb.AppendLine();
        }

        sb.AppendLine("## Spec packet");
        sb.AppendLine();

        return new Outcome(sb.ToString(), null, null);
    }
}
