#if WARDEN
using APIFramework.Components;
using APIFramework.Systems.Physics;

/// <summary>
/// scenario slip &lt;npc-name&gt;
/// Spawns a water-puddle stain on the NPC's tile (or under them if stationary).
/// SlipAndFallSystem evaluates the hazard on the next tick.
/// </summary>
public sealed class SlipSubverb : IScenarioSubverb
{
    public string Name        => "slip";
    public string Usage       => "scenario slip <npc-name|id>";
    public string Description => "Spawn a slip hazard stain under the NPC.";

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (ctx.Host?.Engine == null)
            return "ERROR: Engine not available.";

        if (ctx.MutationApi == null)
            return "ERROR: MutationApi not available.";

        var entity = ScenarioArgParser.FindEntity(args[0], ctx.Host);
        if (entity == null)
            return $"ERROR: Entity '{args[0]}' not found.";

        if (!entity.Has<PositionComponent>())
            return $"ERROR: Entity '{args[0]}' has no PositionComponent.";

        var pos = entity.Get<PositionComponent>();
        int tileX = (int)pos.X;
        int tileZ = (int)pos.Z;

        ctx.MutationApi.SpawnStain(StainTemplates.WaterPuddle, tileX, tileZ);

        string name = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : entity.Id.ToString();

        return $"Stain spawned at ({tileX}, {tileZ}) under '{name}'. SlipAndFallSystem evaluates next tick.";
    }
}
#endif
