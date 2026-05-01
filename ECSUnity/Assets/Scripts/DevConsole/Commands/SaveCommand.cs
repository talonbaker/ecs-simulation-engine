#if WARDEN
// SaveCommand.cs
// Saves the current world state to a named slot via SaveLoadPanel.Save(slot).
//
// Slot names can contain spaces — all arguments after the command name are joined
// with a space to form the slot name. This means:
//   save my slot name    -> saves to slot "my slot name"
//   save autosave-01     -> saves to slot "autosave-01"
//
// SaveLoadPanel.Save is responsible for serialising WorldStateDto + entity component
// data to disk (or PlayerPrefs, depending on implementation). This command does not
// validate whether the save succeeded — if SaveLoadPanel throws, the exception will
// bubble up to DevConsoleCommandDispatcher.
//
// Usage:
//   save <slot-name>
//
// Examples:
//   save before-experiment
//   save slot 1
//
// Return conventions:
//   Plain string on success.
//   "ERROR: ..."  on failure.

public sealed class SaveCommand : IDevConsoleCommand
{
    public string Name        => "save";
    public string Usage       => "save <slot-name>";
    public string Description => "Save the current game state to the named slot.";
    public string[] Aliases   => System.Array.Empty<string>();

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (ctx.SaveLoad == null)
            return "ERROR: SaveLoadPanel not available.";

        // Allow multi-word slot names: "save before experiment" -> "before experiment"
        string slot = string.Join(" ", args);
        ctx.SaveLoad.Save(slot);

        return $"Saved to slot '{slot}'.";
    }
}
#endif
