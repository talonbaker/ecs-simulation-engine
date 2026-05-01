#if WARDEN
// HelpCommand.cs
// Lists all registered dev console commands, or shows detailed usage for a specific command.
// Requires a DevConsoleCommandDispatcher reference so it can enumerate the command registry
// at runtime — this avoids hard-coding any command names here.
//
// Usage:
//   help              — print every registered command with its one-line description
//   help <command>    — print full Usage + Description for that command
//   ? <command>       — alias for 'help <command>'
//
// Return conventions:
//   "INFO: ..."  — informational listing
//   "ERROR: ..."  — unknown command name

using System.Text;

public sealed class HelpCommand : IDevConsoleCommand
{
    public string Name        => "help";
    public string Usage       => "help [command]";
    public string Description => "List all commands, or show usage for a specific command.";
    public string[] Aliases   => new[] { "?" };

    // Injected at registration time so we can walk the live command map.
    private readonly DevConsoleCommandDispatcher _dispatcher;

    public HelpCommand(DevConsoleCommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length > 0)
        {
            // Lookup help for a specific command — check both primary names and aliases.
            string name = args[0].ToLowerInvariant();
            if (_dispatcher.Commands.TryGetValue(name, out var cmd))
                return $"INFO: {cmd.Name}  {cmd.Usage}\n  {cmd.Description}";
            return $"ERROR: Unknown command '{name}'.";
        }

        // Full listing — deduplicate so aliases do not produce double entries.
        var sb   = new StringBuilder();
        var seen = new System.Collections.Generic.HashSet<string>();

        sb.AppendLine("INFO: Available commands:");

        foreach (var cmd in _dispatcher.Commands.Values)
        {
            // Commands are stored once per name/alias key; skip duplicates by canonical name.
            if (!seen.Add(cmd.Name)) continue;
            sb.AppendLine($"  {cmd.Name,-18} {cmd.Description}");
        }

        return sb.ToString().TrimEnd();
    }
}
#endif
