#if WARDEN
// ForceFaintCommand.cs
// Deprecated alias for "scenario faint <npc>" — preserved for muscle-memory and existing tests.
// All logic lives in FaintSubverb; this is a thin shim.

public sealed class ForceFaintCommand : IDevConsoleCommand
{
    public string Name        => "force-faint";
    public string Usage       => "force-faint <npcId|name>";
    public string Description => "Force an NPC to Incapacitated. (Alias for 'scenario faint'.)";
    public string[] Aliases   => System.Array.Empty<string>();

    private static readonly FaintSubverb _handler = new FaintSubverb();

    public string Execute(string[] args, DevCommandContext ctx) =>
        _handler.Execute(args, ctx);
}
#endif
