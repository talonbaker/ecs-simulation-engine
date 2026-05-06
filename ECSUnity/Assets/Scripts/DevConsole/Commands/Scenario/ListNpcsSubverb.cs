#if WARDEN
using System.Text;
using APIFramework.Components;

/// <summary>
/// scenario list-npcs
/// Prints all spawned NPCs with their name, life state, and short id.
/// Surfaced from PT-001-iter-2 (Talon needed to discover NPC names to test
/// other scenario verbs against; the inspector wasn't yet rendering).
/// </summary>
public sealed class ListNpcsSubverb : IScenarioSubverb
{
    public string Name        => "list-npcs";
    public string Usage       => "scenario list-npcs";
    public string Description => "Print all spawned NPC names + life state + id (use to discover targets for other subverbs).";

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (ctx.Host?.Engine?.Entities == null)
            return "ERROR: Engine not available.";

        var sb = new StringBuilder();
        int count = 0;

        foreach (var entity in ctx.Host.Engine.Entities)
        {
            if (!entity.Has<IdentityComponent>()) continue;
            if (!entity.Has<LifeStateComponent>()) continue;  // skip non-NPCs

            string name  = entity.Get<IdentityComponent>().Name;
            var    state = entity.Get<LifeStateComponent>().State;
            string idHex = entity.Id.ToString("N").Substring(0, 8);

            sb.AppendLine($"  {name,-20} {state,-14} ({idHex})");
            count++;
        }

        if (count == 0)
            return "INFO: No NPCs found in the engine. Has the scene booted?";

        return $"NPCs ({count}):\n" + sb.ToString().TrimEnd();
    }
}
#endif
