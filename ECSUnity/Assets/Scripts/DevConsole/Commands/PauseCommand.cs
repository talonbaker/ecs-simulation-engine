#if WARDEN
// PauseCommand.cs
// Pauses the engine by delegating to TimeHudPanel.SetPaused(true).
//
// Preferred path:
//   TimeHudPanel.SetPaused(true) sets Time.timeScale = 0 AND updates the pause-button
//   visual state so the HUD stays in sync. Always prefer this path when the panel is wired.
//
// Fallback path:
//   If ctx.TimeHud is null (e.g., in a headless test run or before HUD initialises),
//   we set Time.timeScale directly. This pauses Unity's physics and FixedUpdate loop
//   (and therefore the engine tick loop) but the HUD will not reflect the paused state.
//
// Usage:
//   pause
//
// Return conventions:
//   Plain string on success.
//   The fallback path returns an "INFO: ..." prefix to flag the degraded mode.

public sealed class PauseCommand : IDevConsoleCommand
{
    public string Name        => "pause";
    public string Usage       => "pause";
    public string Description => "Pause the engine (sets Time.timeScale = 0).";
    public string[] Aliases   => System.Array.Empty<string>();

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (ctx.TimeHud != null)
        {
            // Preferred: panel keeps HUD in sync.
            ctx.TimeHud.SetPaused(true);
            return "Engine paused.";
        }

        // Fallback: direct timeScale manipulation, HUD will be out of sync.
        UnityEngine.Time.timeScale = 0f;
        return "INFO: Engine paused (TimeHudPanel not wired — set Time.timeScale directly). HUD may be out of sync.";
    }
}
#endif
