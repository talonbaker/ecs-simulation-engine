#if WARDEN
using APIFramework.Components;

/// <summary>
/// scenario faint &lt;npc-name&gt;
/// Forces NPC to Incapacitated via faint cause. Alias for the deprecated "force-faint" command.
/// </summary>
public sealed class FaintSubverb : IScenarioSubverb
{
    public string Name        => "faint";
    public string Usage       => "scenario faint <npc-name|id>";
    public string Description => "Force NPC to Incapacitated (faint path; rescuable).";

    private const int DefaultBudget = 600;

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
        if (ls.State == LifeState.Deceased)
            return $"ERROR: '{args[0]}' is already Deceased.";

        ls.State                   = LifeState.Incapacitated;
        ls.LastTransitionTick      = ctx.Host.TickCount;
        ls.IncapacitatedTickBudget = DefaultBudget;
        ls.PendingDeathCause       = CauseOfDeath.Unknown;
        entity.Add(ls);

        string name = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : entity.Id.ToString();

        return $"'{name}' fainted. Tick budget: {DefaultBudget} before Deceased (unless rescued).";
    }
}
#endif
