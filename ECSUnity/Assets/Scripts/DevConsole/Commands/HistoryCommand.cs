#if WARDEN
// HistoryCommand.cs
// Prints the last N entries from the console's output history.
//
// DevConsolePanel.GetHistory() returns the internal history list. Each entry is a
// ConsoleHistoryEntry (or equivalent) with at minimum a Text field containing the
// raw output string (command + result).
//
// Only the last 20 entries are shown to avoid flooding the visible panel with its
// own history. Change MaxEntries if you need a longer or shorter window.
//
// Why is this useful?
//   When a command produces multi-line output that has scrolled off the visible area,
//   'history' lets the developer retrieve it without scrolling. It also serves as a
//   quick audit trail of commands run during a session.
//
// Usage:
//   history
//
// Return conventions:
//   "INFO: ..."  listing on success.
//   "INFO: No history."  if no commands have been run.
//   "ERROR: ..."  if the console panel is not wired.

using System.Text;

public sealed class HistoryCommand : IDevConsoleCommand
{
    public string Name        => "history";
    public string Usage       => "history";
    public string Description => "Print the navigation history (last commands entered).";
    public string[] Aliases   => System.Array.Empty<string>();

    // Maximum number of history entries to display.
    private const int MaxEntries = 20;

    public string Execute(string[] args, DevCommandContext ctx)
    {
        var panel = ctx.Console;
        if (panel == null)
            return "ERROR: Console panel not available.";

        var history = panel.GetHistory();
        if (history == null || history.Count == 0)
            return "INFO: No history.";

        var sb    = new StringBuilder();
        int start = System.Math.Max(0, history.Count - MaxEntries);

        sb.AppendLine($"INFO: Console history (last {history.Count - start} of {history.Count} entries):");

        for (int i = start; i < history.Count; i++)
        {
            // ConsoleEntry is a struct (value type) — no null-conditional. Text
            // itself is a non-null string set at construction.
            string line = history[i].Text ?? "(empty)";
            sb.AppendLine($"  [{i,3}] {line}");
        }

        return sb.ToString().TrimEnd();
    }
}
#endif
