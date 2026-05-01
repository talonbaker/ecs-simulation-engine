#if WARDEN
// QuitCommand.cs
// Closes the developer console panel. This does NOT quit the application.
//
// DevConsolePanel.Close() is expected to:
//   - Hide the panel (SetActive(false) or equivalent).
//   - Return input focus to the game (re-enable PlayerInputActions or equivalent).
//   - NOT pause, save, or modify engine state in any way.
//
// The return value is null so no output line is written before the panel closes —
// the close animation (if any) happens on a clean output area.
//
// Aliases:
//   exit  — mirrors shell conventions
//   close — more descriptive alternative
//
// Usage:
//   quit
//   exit    (alias)
//   close   (alias)
//
// Return conventions:
//   null — intentionally silent.

public sealed class QuitCommand : IDevConsoleCommand
{
    public string Name        => "quit";
    public string Usage       => "quit";
    public string Description => "Close the developer console (same as pressing ~).";
    public string[] Aliases   => new[] { "exit", "close" };

    public string Execute(string[] args, DevCommandContext ctx)
    {
        // Close the panel. Input focus is returned to the game by DevConsolePanel.Close().
        // Returning null means the dispatcher writes nothing — the panel closes silently.
        ctx.Console?.Close();
        return null;
    }
}
#endif
