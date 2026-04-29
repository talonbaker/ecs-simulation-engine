#if WARDEN
// ClearCommand.cs
// Clears the console output history. Returns null so no new line is appended after
// the clear — the result is a completely empty console output area.
//
// DevConsolePanel.ClearHistory() is expected to:
//   - Remove all logged lines from the scroll view.
//   - Reset any internal line buffer.
//   - NOT clear the command input field (the user may be mid-typing).
//
// The alias "cls" mirrors the Windows command-line convention, useful for developers
// who instinctively type it.
//
// Usage:
//   clear
//   cls      (alias)
//
// Return conventions:
//   null — intentionally silent; the act of clearing is its own feedback.

public sealed class ClearCommand : IDevConsoleCommand
{
    public string Name        => "clear";
    public string Usage       => "clear";
    public string Description => "Clear the console output history.";
    public string[] Aliases   => new[] { "cls" };

    public string Execute(string[] args, DevCommandContext ctx)
    {
        // ClearHistory wipes the visible output panel. The null return means the
        // dispatcher will not append any result line, leaving the console truly empty.
        ctx.Console?.ClearHistory();
        return null;
    }
}
#endif
