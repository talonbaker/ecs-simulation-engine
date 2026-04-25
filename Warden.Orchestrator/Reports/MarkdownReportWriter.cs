using System.Text;

namespace Warden.Orchestrator.Reports;

/// <summary>
/// Renders an in-memory <see cref="Report"/> as Markdown following the fixed
/// layout defined in WP-12 and <c>docs/c2-infrastructure/report-template.md</c>.
/// </summary>
public static class MarkdownReportWriter
{
    public static string Render(Report report)
    {
        var sb = new StringBuilder();

        if (report.IsEarlyTermination)
        {
            sb.AppendLine("> **Run terminated early** — partial results only.");
            sb.AppendLine();
        }

        // ── Header ────────────────────────────────────────────────────────────────

        sb.AppendLine($"# Mission Report — {report.MissionId}");
        sb.AppendLine();
        sb.AppendLine($"**Run id:** {report.RunId}");
        sb.AppendLine($"**Started:** {report.StartedUtc:yyyy-MM-ddTHH:mm:ssZ}");

        if (report.EndedUtc.HasValue)
        {
            var dur = report.EndedUtc.Value - report.StartedUtc;
            sb.AppendLine(
                $"**Ended:**   {report.EndedUtc.Value:yyyy-MM-ddTHH:mm:ssZ} ({FormatDuration(dur)})");
        }
        else
        {
            sb.AppendLine("**Ended:**   (incomplete)");
        }

        sb.AppendLine($"**Terminal outcome:** {report.TerminalOutcome}");
        sb.AppendLine($"**Exit code:** {report.ExitCode}");
        sb.AppendLine($"**Total spend:** ${report.TotalSpendUsd:F4}");
        sb.AppendLine($"**Budget:** {FormatBudget(report.BudgetUsd, report.TotalSpendUsd)}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // ── Summary ───────────────────────────────────────────────────────────────

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine(BuildSummaryText(report));
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // ── Tier 2 — Sonnet results ───────────────────────────────────────────────

        sb.AppendLine("## Tier 2 — Sonnet results");
        sb.AppendLine();
        sb.AppendLine("| Spec | Worker | Outcome | ATs passed | Spend | Notes |");
        sb.AppendLine("|:---|:---|:---|:---:|---:|:---|");
        foreach (var s in report.Sonnets)
        {
            var notes = s.Notes ?? "—";
            sb.AppendLine(
                $"| {s.SpecId} | {s.WorkerId} | {s.Outcome} | {s.AtsPassed}/{s.AtsTotal} " +
                $"| ${s.SpendUsd:F4} | {notes} |");
        }
        sb.AppendLine();

        foreach (var s in report.Sonnets)
        {
            var title = s.SpecTitle.Length > 0 ? s.SpecTitle : s.SpecId;
            sb.AppendLine($"### {s.SpecId} — {title}");
            sb.AppendLine();

            if (s.WorktreePath is not null)
                sb.AppendLine($"- Worktree: `{s.WorktreePath}`");

            if (s.Diff is { } d)
            {
                var files = d.FilesAdded + d.FilesModified + d.FilesDeleted;
                var parts = new List<string>();
                if (files > 0)     parts.Add($"{files} file{(files != 1 ? "s" : "")} modified");
                if (d.LinesAdded   > 0) parts.Add($"{d.LinesAdded} lines added");
                if (d.LinesRemoved > 0) parts.Add($"{d.LinesRemoved} lines removed");
                if (parts.Count > 0)
                    sb.AppendLine($"- Diff summary: {string.Join(", ", parts)}");
            }

            if (s.AtResults.Count > 0)
            {
                sb.AppendLine("- Acceptance test results:");
                foreach (var at in s.AtResults)
                {
                    var mark = at.Passed ? "✓" : "✗";
                    var note = at.Notes is not null ? $" — {at.Notes}" : string.Empty;
                    sb.AppendLine($"    - {at.Id} {mark}{note}");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();

        // ── Tier 3 — Haiku results ────────────────────────────────────────────────

        sb.AppendLine("## Tier 3 — Haiku results");
        sb.AppendLine();

        var sonnetsWithHaiku = report.Sonnets.Where(s => s.Haikus.Count > 0).ToList();
        if (sonnetsWithHaiku.Count == 0)
        {
            sb.AppendLine("No Haiku scenarios run.");
        }
        else
        {
            sb.AppendLine("Grouped by parent spec.");
            sb.AppendLine();

            foreach (var s in sonnetsWithHaiku)
            {
                var n = s.Haikus.Count;
                sb.AppendLine($"### Under {s.SpecId} ({n} scenario{(n != 1 ? "s" : "")})");
                sb.AppendLine();
                sb.AppendLine("| Scenario | Seed | Outcome | Assertions | Duration (gs) | Spend |");
                sb.AppendLine("|:---|---:|:---|:---:|---:|---:|");
                foreach (var h in s.Haikus)
                {
                    sb.AppendLine(
                        $"| {h.ScenarioId} | {h.Seed} | {h.Outcome} " +
                        $"| {h.AssertionsPassed}/{h.AssertionsTotal} " +
                        $"| {h.DurationGameSeconds:F0} | ${h.SpendUsd:F4} |");
                }
                sb.AppendLine();

                var allMetrics = s.Haikus
                    .Where(h => h.KeyMetrics != null)
                    .SelectMany(h => h.KeyMetrics!)
                    .GroupBy(kv => kv.Key, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Select(kv => kv.Value).ToList());

                if (allMetrics.Count > 0)
                {
                    sb.AppendLine("Aggregate key metrics across scenarios:");
                    sb.AppendLine();
                    sb.AppendLine("| Metric | Mean | Min | Max | Stddev |");
                    sb.AppendLine("|:---|---:|---:|---:|---:|");
                    foreach (var (metric, vals) in allMetrics.OrderBy(x => x.Key))
                    {
                        if (vals.Count == 0) continue;
                        var mean   = vals.Average();
                        var min    = vals.Min();
                        var max    = vals.Max();
                        var stddev = Math.Sqrt(vals.Average(v => Math.Pow(v - mean, 2)));
                        sb.AppendLine(
                            $"| {metric} | {mean:F1} | {min:F1} | {max:F1} | {stddev:F1} |");
                    }
                    sb.AppendLine();
                }
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();

        // ── Cost summary ──────────────────────────────────────────────────────────

        sb.AppendLine("## Cost summary");
        sb.AppendLine();
        sb.AppendLine("| Tier | Calls | Input tok | Cached read tok | Output tok | Spend |");
        sb.AppendLine("|:---|---:|---:|---:|---:|---:|");

        var sc = report.Cost.Sonnet;
        var hc = report.Cost.Haiku;
        sb.AppendLine(
            $"| Sonnet | {sc.Calls} | {sc.InputTokens:N0} | {sc.CachedReadTokens:N0} " +
            $"| {sc.OutputTokens:N0} | ${sc.SpendUsd:F4} |");
        sb.AppendLine(
            $"| Haiku (batched) | {hc.Calls} | {hc.InputTokens:N0} | {hc.CachedReadTokens:N0} " +
            $"| {hc.OutputTokens:N0} | ${hc.SpendUsd:F4} |");

        var tc = sc.Calls + hc.Calls;
        var ti = sc.InputTokens + hc.InputTokens;
        var tch = sc.CachedReadTokens + hc.CachedReadTokens;
        var to  = sc.OutputTokens + hc.OutputTokens;
        var ts  = sc.SpendUsd + hc.SpendUsd;
        sb.AppendLine(
            $"| **Total** | **{tc}** | **{ti:N0}** | **{tch:N0}** " +
            $"| **{to:N0}** | **${ts:F4}** |");

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // ── Notable events ────────────────────────────────────────────────────────

        sb.AppendLine("## Notable events");
        sb.AppendLine();
        if (report.Events.Count == 0)
        {
            sb.AppendLine("(none)");
        }
        else
        {
            foreach (var e in report.Events)
            {
                var detail = e.Detail is not null ? $" {e.Detail}" : string.Empty;
                sb.AppendLine($"- {e.Timestamp:HH:mm:ss} {e.Kind}{detail}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // ── Artifacts ─────────────────────────────────────────────────────────────

        sb.AppendLine("## Artifacts");
        sb.AppendLine();
        sb.AppendLine("- Ledger: [cost-ledger.jsonl](./cost-ledger.jsonl)");
        sb.AppendLine("- Events: [events.jsonl](./events.jsonl)");
        sb.AppendLine("- JSON mirror: [report.json](./report.json)");

        return sb.ToString();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalMinutes < 1) return $"{(int)ts.TotalSeconds}s";
        return $"{(int)ts.TotalMinutes}m {ts.Seconds:D2}s";
    }

    private static string FormatBudget(decimal budget, decimal spent)
    {
        if (budget >= decimal.MaxValue / 2) return "unlimited";
        var remaining = budget - spent;
        var pct       = budget > 0 ? Math.Round(remaining / budget * 100, 0) : 0m;
        return $"${budget:F2} ({pct:F0}% remaining)";
    }

    private static string BuildSummaryText(Report report)
    {
        var sonnetCount = report.Sonnets.Count;
        var haikuCount  = report.Sonnets.Sum(s => s.Haikus.Count);

        string outcomeText = report.TerminalOutcome switch
        {
            "ok"              => "All passed acceptance.",
            "failed"          => $"{report.Sonnets.Count(s => s.Outcome == "failed")} spec(s) failed acceptance.",
            "budget-exceeded" => "Run halted: budget exceeded.",
            _                 => report.IsEarlyTermination
                                    ? "Run terminated early."
                                    : $"{report.Sonnets.Count(s => s.Outcome == "blocked")} spec(s) blocked."
        };

        var violations = report.Sonnets
            .SelectMany(s => s.Haikus)
            .Count(h => h.KeyMetrics != null
                && h.KeyMetrics.TryGetValue("violation count", out var v)
                && v > 0);

        var invariantText = violations > 0
            ? $"{violations} invariant violation{(violations != 1 ? "s" : "")}."
            : "No invariant violations.";

        return $"1 mission, {sonnetCount} Sonnet spec{(sonnetCount != 1 ? "s" : "")}, " +
               $"{haikuCount} Haiku scenario{(haikuCount != 1 ? "s" : "")}. " +
               $"{outcomeText} {invariantText}";
    }
}
