#if WARDEN
using APIFramework.Components;

/// <summary>
/// scenario lockout &lt;npc-name&gt;
/// Attaches LockedInComponent to the NPC, starting the starvation countdown.
/// Errors if the NPC is not inside a room with defined bounds.
/// </summary>
public sealed class LockoutSubverb : IScenarioSubverb
{
    public string Name        => "lockout";
    public string Usage       => "scenario lockout <npc-name|id>";
    public string Description => "Lock the NPC in their current room; starvation timer starts.";

    private const int StarvationDayBudget = 3;

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (ctx.Host?.Engine == null)
            return "ERROR: Engine not available.";

        var entity = ScenarioArgParser.FindEntity(args[0], ctx.Host);
        if (entity == null)
            return $"ERROR: Entity '{args[0]}' not found.";

        if (!entity.Has<LifeStateComponent>())
            return $"ERROR: Entity '{args[0]}' has no LifeStateComponent.";

        var ls = entity.Get<LifeStateComponent>();
        if (ls.State != LifeState.Alive)
            return $"ERROR: NPC is not Alive (state: {ls.State}).";

        if (!entity.Has<PositionComponent>())
            return $"ERROR: Entity '{args[0]}' has no PositionComponent.";

        var pos = entity.Get<PositionComponent>();
        int tileX = (int)pos.X;
        int tileZ = (int)pos.Z;

        // Check if the NPC is inside any room with defined bounds.
        bool inRoom = false;
        foreach (var e in ctx.Host.Engine.Entities)
        {
            if (!e.Has<RoomTag>() || !e.Has<RoomComponent>()) continue;
            var room = e.Get<RoomComponent>();
            if (room.Bounds.Contains(tileX, tileZ)) { inRoom = true; break; }
        }

        if (!inRoom)
        {
            string npcName = entity.Has<IdentityComponent>()
                ? entity.Get<IdentityComponent>().Name
                : entity.Id.ToString();
            return $"INFO: {npcName} is not in a lockable room.";
        }

        entity.Add(new LockedInComponent
        {
            FirstDetectedTick    = ctx.Host.TickCount,
            StarvationTickBudget = StarvationDayBudget,
        });

        string name = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : entity.Id.ToString();

        return $"'{name}' is locked in. Starvation budget: {StarvationDayBudget} game-days.";
    }
}
#endif
