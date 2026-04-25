namespace Warden.Orchestrator.Persistence;

/// <summary>
/// Single source of truth for every path in the <c>./runs/&lt;runId&gt;/</c> tree.
/// All consumers build paths through this class so renaming a file means one edit.
/// </summary>
public static class RunLayout
{
    public static string RunRoot(string runsRoot, string runId)
        => Path.Combine(runsRoot, runId);

    // -- Run-level files ----------------------------------------------------------

    public static string MissionFile(string runsRoot, string runId)
        => Path.Combine(RunRoot(runsRoot, runId), "mission.md");

    public static string EventsFile(string runsRoot, string runId)
        => Path.Combine(RunRoot(runsRoot, runId), "events.jsonl");

    public static string CostLedgerFile(string runsRoot, string runId)
        => Path.Combine(RunRoot(runsRoot, runId), "cost-ledger.jsonl");

    public static string ReportMdFile(string runsRoot, string runId)
        => Path.Combine(RunRoot(runsRoot, runId), "report.md");

    public static string ReportJsonFile(string runsRoot, string runId)
        => Path.Combine(RunRoot(runsRoot, runId), "report.json");

    // -- Sonnet worker files ------------------------------------------------------

    public static string SonnetDir(string runsRoot, string runId, string sonnetId)
        => Path.Combine(RunRoot(runsRoot, runId), sonnetId);

    public static string SpecFile(string runsRoot, string runId, string sonnetId)
        => Path.Combine(SonnetDir(runsRoot, runId, sonnetId), "spec.json");

    public static string PromptFile(string runsRoot, string runId, string sonnetId)
        => Path.Combine(SonnetDir(runsRoot, runId, sonnetId), "prompt.txt");

    public static string RawResponseFile(string runsRoot, string runId, string sonnetId)
        => Path.Combine(SonnetDir(runsRoot, runId, sonnetId), "response.raw.json");

    public static string ResultFile(string runsRoot, string runId, string sonnetId)
        => Path.Combine(SonnetDir(runsRoot, runId, sonnetId), "result.json");

    public static string HaikuBatchFile(string runsRoot, string runId, string sonnetId)
        => Path.Combine(SonnetDir(runsRoot, runId, sonnetId), "haiku-batch.json");

    // -- Haiku worker files -------------------------------------------------------

    public static string HaikuDir(string runsRoot, string runId, string sonnetId, string haikuId)
        => Path.Combine(SonnetDir(runsRoot, runId, sonnetId), haikuId);

    public static string HaikuScenarioFile(string runsRoot, string runId, string sonnetId, string haikuId)
        => Path.Combine(HaikuDir(runsRoot, runId, sonnetId, haikuId), "scenario.json");

    public static string HaikuPromptFile(string runsRoot, string runId, string sonnetId, string haikuId)
        => Path.Combine(HaikuDir(runsRoot, runId, sonnetId, haikuId), "prompt.txt");

    public static string HaikuRawResponseFile(string runsRoot, string runId, string sonnetId, string haikuId)
        => Path.Combine(HaikuDir(runsRoot, runId, sonnetId, haikuId), "response.raw.json");

    public static string HaikuResultFile(string runsRoot, string runId, string sonnetId, string haikuId)
        => Path.Combine(HaikuDir(runsRoot, runId, sonnetId, haikuId), "result.json");

    public static string HaikuTelemetryFile(string runsRoot, string runId, string sonnetId, string haikuId)
        => Path.Combine(HaikuDir(runsRoot, runId, sonnetId, haikuId), "telemetry.jsonl");
}
