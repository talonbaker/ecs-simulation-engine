#if WARDEN
// LoadCommand.cs
// Restores world state from a named slot via SaveLoadPanel.Load(slot).
//
// This is a destructive operation — the current in-memory world state is replaced
// by whatever was serialised into the named slot. Any unsaved changes will be lost.
// There is no confirmation prompt; the dev console is a power-user tool.
//
// Like SaveCommand, multi-word slot names are supported by joining all arguments:
//   load before-experiment          -> loads slot "before-experiment"
//   load slot 1                     -> loads slot "slot 1"
//
// SaveLoadPanel.Load is responsible for deserialisation and entity reconstruction.
// This command does not validate whether the slot exists — an error from SaveLoadPanel
// will surface as an exception to the dispatcher.
//
// Usage:
//   load <slot-name>
//
// Examples:
//   load before-experiment
//   load autosave-01
//
// Return conventions:
//   Plain string on success (acknowledges the load was issued, not that it completed).
//   "ERROR: ..."  on failure.

public sealed class LoadCommand : IDevConsoleCommand
{
    public string Name        => "load";
    public string Usage       => "load <slot-name>";
    public string Description => "Load game state from the named slot.";
    public string[] Aliases   => System.Array.Empty<string>();

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (ctx.SaveLoad == null)
            return "ERROR: SaveLoadPanel not available.";

        string slot = string.Join(" ", args);
        ctx.SaveLoad.Load(slot);

        return $"Loaded slot '{slot}'.";
    }
}
#endif
