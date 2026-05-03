#if WARDEN
/// <summary>
/// Dev console verb: `introspect &lt;on|off|selected|all&gt;`
///
/// Sets the NpcIntrospectionOverlay mode explicitly from the console.
/// `on` is an alias for `all`.
///
/// Usage:
///   introspect on       → All mode
///   introspect all      → All mode
///   introspect selected → Selected mode
///   introspect off      → Off mode
///
/// Return conventions:
///   Success string on success.
///   "ERROR: …" on bad args or missing overlay.
/// </summary>
public sealed class IntrospectCommand : IDevConsoleCommand
{
    public string   Name        => "introspect";
    public string   Usage       => "introspect <on|off|selected|all>";
    public string   Description => "Set the NPC introspection overlay mode.";
    public string[] Aliases     => System.Array.Empty<string>();

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (ctx.Overlay == null)
            return "ERROR: NpcIntrospectionOverlay not found in scene. Add it to the scene first.";

        NpcIntrospectionMode? mode = args[0].ToLowerInvariant() switch
        {
            "on"       => NpcIntrospectionMode.All,
            "all"      => NpcIntrospectionMode.All,
            "off"      => NpcIntrospectionMode.Off,
            "selected" => NpcIntrospectionMode.Selected,
            _          => null,
        };

        if (mode == null)
            return $"ERROR: Unknown mode '{args[0]}'. Use: on | off | selected | all";

        ctx.Overlay.SetMode(mode.Value);
        return $"Introspection overlay: {mode.Value}";
    }
}
#endif
