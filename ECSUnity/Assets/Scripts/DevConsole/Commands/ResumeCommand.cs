#if WARDEN
// ResumeCommand.cs
// Resumes the engine after a pause by delegating to TimeHudPanel.SetPaused(false).
//
// Preferred path:
//   TimeHudPanel.SetPaused(false) restores Time.timeScale to its pre-pause value
//   AND updates the HUD pause-button visual. Always prefer this path when the panel
//   is wired.
//
// Fallback path:
//   If ctx.TimeHud is null, we set Time.timeScale = 1f directly. This resumes Unity's
//   FixedUpdate loop at 1x speed. Note: if the simulation was paused at a custom time
//   scale (e.g., 2x), the fallback always restores to 1x — the custom scale is lost.
//   Use the preferred path (TimeHudPanel) to avoid this.
//
// Usage:
//   resume
//
// Return conventions:
//   Plain string on success.
//   The fallback path returns an "INFO: ..." prefix to flag the degraded mode.

public sealed class ResumeCommand : IDevConsoleCommand
{
    public string Name        => "resume";
    public string Usage       => "resume";
    public string Description => "Resume the engine after pause.";
    public string[] Aliases   => System.Array.Empty<string>();

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (ctx.TimeHud != null)
        {
            // Preferred: panel restores pre-pause time scale and syncs HUD.
            ctx.TimeHud.SetPaused(false);
            return "Engine resumed.";
        }

        // Fallback: direct timeScale reset to 1x. Custom time scales are not preserved.
        UnityEngine.Time.timeScale = 1f;
        return "INFO: Engine resumed (TimeHudPanel not wired — set Time.timeScale = 1f directly). " +
               "Custom time scale, if any, has been reset to 1x.";
    }
}
#endif
