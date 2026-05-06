#if WARDEN
// ForceKillCommand.cs
// Deprecated alias for "scenario kill <npc> [cause]" — preserved for muscle-memory and existing tests.
// All logic lives in KillSubverb; this is a thin shim.

public sealed class ForceKillCommand : IDevConsoleCommand
{
    public string Name        => "force-kill";
    public string Usage       => "force-kill <npcId|name> [cause]";
    public string Description => "Force an NPC to Deceased. (Alias for 'scenario kill'.)";
    public string[] Aliases   => System.Array.Empty<string>();

    private static readonly KillSubverb _handler = new KillSubverb();

    public string Execute(string[] args, DevCommandContext ctx) =>
        _handler.Execute(args, ctx);
}
#endif
