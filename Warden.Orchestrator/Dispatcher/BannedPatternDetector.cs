using System.Text.RegularExpressions;

namespace Warden.Orchestrator.Dispatcher;

/// <summary>
/// Static analyser-lite: scans a git unified diff for patterns that indicate
/// a worker has violated SRD §4.1 or §8.1 (no runtime LLM calls, no rogue
/// HTTP clients, no recursive orchestrator calls, etc.).
///
/// The false-positive rate is secondary to the zero-false-negative guarantee
/// on the six patterns listed in WP-11.  A match escalates the worker's result
/// to <c>blocked/tool-error</c> regardless of the self-reported outcome.
/// </summary>
public static class BannedPatternDetector
{
    // Projects whose code is allowed to use HTTP / Anthropic client types.
    private static readonly string[] _allowedHttpProjects =
        { "Warden.Anthropic", "Warden.Orchestrator" };

    // csproj files that must not gain new package/project references.
    private static readonly string[] _forbiddenCsprojNames =
        { "APIFramework.csproj", "ECSCli.csproj", "ECSVisualizer.csproj" };

    // Orchestrator type name fragments — a Task.Run containing any of these
    // signals recursive dispatch back into the orchestrator tier.
    private static readonly string[] _orchestratorTypeFragments =
        { "Dispatcher", "Orchestrator", "Escalator", "Aggregator", "Scheduler" };

    // -- Public API ------------------------------------------------------------

    /// <summary>Returns true if the diff contains ≥1 banned pattern.</summary>
    public static bool HasBannedPattern(string diff) => Scan(diff).Count > 0;

    /// <summary>
    /// Returns every banned-pattern detection found in the diff.
    /// The diff must be standard git unified-diff output (from <c>git diff</c> or
    /// <c>git show</c>).  Only lines prefixed with <c>+</c> (additions) are checked.
    /// </summary>
    public static IReadOnlyList<BannedDetection> Scan(string diff)
    {
        var detections   = new List<BannedDetection>();
        var currentFile  = string.Empty;

        foreach (var rawLine in diff.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            // Track the current file being diffed.
            if (line.StartsWith("+++ b/", StringComparison.Ordinal))
            {
                currentFile = NormalisePath(line[6..]);
                continue;
            }
            if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                // e.g. "+++ /dev/null" for deletions — no file to track.
                currentFile = string.Empty;
                continue;
            }
            if (line.StartsWith("diff --git ", StringComparison.Ordinal) ||
                line.StartsWith("--- ",         StringComparison.Ordinal) ||
                line.StartsWith("@@",           StringComparison.Ordinal))
            {
                continue;
            }

            // Only inspect added lines.
            if (!line.StartsWith("+", StringComparison.Ordinal))
                continue;

            var addedContent = line[1..];
            CheckAddedLine(currentFile, addedContent, detections);
        }

        return detections;
    }

    // -- Per-line checks -------------------------------------------------------

    private static void CheckAddedLine(
        string file, string content, List<BannedDetection> detections)
    {
        CheckPattern1_HttpNamespace(file, content, detections);
        CheckPattern2_ClientConstruction(file, content, detections);
        CheckPattern3_ProcessStart(file, content, detections);
        CheckPattern4_ApiKeyInWardenCode(file, content, detections);
        CheckPattern5_ForbiddenCsprojDependency(file, content, detections);
        CheckPattern6_TaskRunCallsOrchestrator(file, content, detections);
    }

    // Pattern 1 — new `using System.Net.Http;` outside the two allowed projects.
    private static void CheckPattern1_HttpNamespace(
        string file, string content, List<BannedDetection> detections)
    {
        if (!content.Contains("using System.Net.Http", StringComparison.Ordinal))
            return;

        if (IsInAllowedHttpProject(file))
            return;

        detections.Add(new BannedDetection(
            BannedPatternKind.HttpNamespaceOutsideWarden, file, content));
    }

    // Pattern 2 — new construction of AnthropicClient, HttpClient, or
    // WebSocketClient outside the two allowed projects.
    private static readonly Regex _clientCtorPattern = new(
        @"new\s+(AnthropicClient|HttpClient|WebSocketClient)\s*[\(\{]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static void CheckPattern2_ClientConstruction(
        string file, string content, List<BannedDetection> detections)
    {
        if (!_clientCtorPattern.IsMatch(content))
            return;

        if (IsInAllowedHttpProject(file))
            return;

        detections.Add(new BannedDetection(
            BannedPatternKind.ClientConstructionOutsideWarden, file, content));
    }

    // Pattern 3 — new Process.Start(...) that does not invoke ECSCli.
    private static readonly Regex _processStartPattern = new(
        @"Process\.Start\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static void CheckPattern3_ProcessStart(
        string file, string content, List<BannedDetection> detections)
    {
        if (!_processStartPattern.IsMatch(content))
            return;

        // Allowed only if the same line references ECSCli by name.
        if (content.Contains("ECSCli", StringComparison.OrdinalIgnoreCase))
            return;

        detections.Add(new BannedDetection(
            BannedPatternKind.UnauthorizedProcessStart, file, content));
    }

    // Pattern 4 — ANTHROPIC_API_KEY appears in any Warden.* file except
    // Warden.Orchestrator/Program.cs (the one authorised reader).
    private static void CheckPattern4_ApiKeyInWardenCode(
        string file, string content, List<BannedDetection> detections)
    {
        if (!content.Contains("ANTHROPIC_API_KEY", StringComparison.Ordinal))
            return;

        if (!IsInWardenProject(file))
            return;

        // The only authorised location is Warden.Orchestrator/Program.cs.
        if (file.EndsWith("Warden.Orchestrator/Program.cs", StringComparison.OrdinalIgnoreCase))
            return;

        detections.Add(new BannedDetection(
            BannedPatternKind.ApiKeyInWardenCode, file, content));
    }

    // Pattern 5 — new <PackageReference> or <ProjectReference> inside the
    // three engine-facing csproj files.
    private static readonly Regex _referenceTagPattern = new(
        @"<(PackageReference|ProjectReference)\s",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static void CheckPattern5_ForbiddenCsprojDependency(
        string file, string content, List<BannedDetection> detections)
    {
        if (!_referenceTagPattern.IsMatch(content))
            return;

        var fileName = Path.GetFileName(file);
        if (!_forbiddenCsprojNames.Any(n =>
                string.Equals(n, fileName, StringComparison.OrdinalIgnoreCase)))
            return;

        detections.Add(new BannedDetection(
            BannedPatternKind.ForbiddenCsprojDependency, file, content));
    }

    // Pattern 6 — new Task.Run call that refers back into an orchestrator type.
    private static readonly Regex _taskRunPattern = new(
        @"\bTask\.Run\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static void CheckPattern6_TaskRunCallsOrchestrator(
        string file, string content, List<BannedDetection> detections)
    {
        if (!_taskRunPattern.IsMatch(content))
            return;

        foreach (var fragment in _orchestratorTypeFragments)
        {
            if (content.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                detections.Add(new BannedDetection(
                    BannedPatternKind.TaskRunCallsOrchestrator, file, content));
                return;
            }
        }
    }

    // -- Helpers ---------------------------------------------------------------

    private static bool IsInAllowedHttpProject(string normalisedPath)
        => _allowedHttpProjects.Any(p =>
            normalisedPath.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase) ||
            normalisedPath.Contains("/" + p + "/", StringComparison.OrdinalIgnoreCase));

    private static bool IsInWardenProject(string normalisedPath)
        => normalisedPath.StartsWith("Warden.", StringComparison.OrdinalIgnoreCase) ||
           normalisedPath.Contains("/Warden.", StringComparison.OrdinalIgnoreCase);

    private static string NormalisePath(string path)
        => path.Replace('\\', '/').Trim();
}

// -- Supporting types ----------------------------------------------------------

/// <summary>
/// Identifies which of the six banned patterns was matched.
/// </summary>
public enum BannedPatternKind
{
    /// <summary>Pattern 1 — <c>using System.Net.Http</c> outside Warden.Anthropic/Warden.Orchestrator.</summary>
    HttpNamespaceOutsideWarden,

    /// <summary>Pattern 2 — new AnthropicClient/HttpClient/WebSocketClient outside the two allowed projects.</summary>
    ClientConstructionOutsideWarden,

    /// <summary>Pattern 3 — <c>Process.Start(</c> without ECSCli on the same line.</summary>
    UnauthorizedProcessStart,

    /// <summary>Pattern 4 — <c>ANTHROPIC_API_KEY</c> in a Warden.* file other than Warden.Orchestrator/Program.cs.</summary>
    ApiKeyInWardenCode,

    /// <summary>Pattern 5 — new package/project reference added to a forbidden engine csproj.</summary>
    ForbiddenCsprojDependency,

    /// <summary>Pattern 6 — <c>Task.Run</c> that invokes an orchestrator type (recursive dispatch signal).</summary>
    TaskRunCallsOrchestrator
}

/// <summary>One banned-pattern detection from <see cref="BannedPatternDetector.Scan"/>.</summary>
public sealed record BannedDetection(
    BannedPatternKind Kind,
    string            FilePath,
    string            AddedLine);
