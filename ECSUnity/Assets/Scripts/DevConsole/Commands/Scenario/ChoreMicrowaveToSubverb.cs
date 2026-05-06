#if WARDEN
using APIFramework.Components;

/// <summary>
/// scenario chore-microwave-to &lt;npc-name&gt;
/// Forces this game-week's microwave-cleaning chore assignment to the named NPC,
/// overriding the rotation order.
/// </summary>
public sealed class ChoreMicrowaveToSubverb : IScenarioSubverb
{
    public string Name        => "chore-microwave-to";
    public string Usage       => "scenario chore-microwave-to <npc-name|id>";
    public string Description => "Force the CleanMicrowave chore to a specific NPC.";

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (ctx.Host?.Engine == null)
            return "ERROR: Engine not available.";

        var npc = ScenarioArgParser.FindEntity(args[0], ctx.Host);
        if (npc == null)
            return $"ERROR: Entity '{args[0]}' not found.";

        if (!npc.Has<LifeStateComponent>() ||
            npc.Get<LifeStateComponent>().State != LifeState.Alive)
            return $"ERROR: '{args[0]}' is not Alive.";

        // Find the CleanMicrowave chore entity.
        APIFramework.Core.Entity choreEntity = null;
        foreach (var e in ctx.Host.Engine.Entities)
        {
            if (!e.Has<ChoreComponent>()) continue;
            if (e.Get<ChoreComponent>().Kind == ChoreKind.CleanMicrowave)
            {
                choreEntity = e;
                break;
            }
        }

        if (choreEntity == null)
            return "ERROR: No CleanMicrowave chore entity found in this world.";

        var chore = choreEntity.Get<ChoreComponent>();
        chore.CurrentAssigneeId = npc.Id;
        choreEntity.Add(chore);

        string name = npc.Has<IdentityComponent>()
            ? npc.Get<IdentityComponent>().Name
            : npc.Id.ToString();

        return $"Microwave-cleaning chore assigned to '{name}'.";
    }
}
#endif
