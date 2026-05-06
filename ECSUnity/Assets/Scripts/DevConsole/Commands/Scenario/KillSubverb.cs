#if WARDEN
using APIFramework.Components;

/// <summary>
/// scenario kill &lt;npc-name&gt; [cause]
/// Pushes NPC to Deceased with the specified cause. Triggers bereavement cascade.
/// Also backs the deprecated "force-kill" command alias.
/// </summary>
public sealed class KillSubverb : IScenarioSubverb
{
    public string Name        => "kill";
    public string Usage       => "scenario kill <npc-name|id> [cause]  (cause: Choked|SlippedAndFell|StarvedAlone|Died)";
    public string Description => "Push NPC to Deceased immediately. Bereavement cascade fires.";

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
            return $"ERROR: Entity '{args[0]}' has no LifeStateComponent — is it a human NPC?";

        // "Died" is an alias for Unknown (the generic cause).
        CauseOfDeath cause = CauseOfDeath.Unknown;
        if (args.Length > 1)
        {
            string causeArg = args[1];
            if (string.Equals(causeArg, "Died", System.StringComparison.OrdinalIgnoreCase))
                cause = CauseOfDeath.Unknown;
            else if (!System.Enum.TryParse(causeArg, ignoreCase: true, out cause))
                return $"ERROR: Unknown cause '{causeArg}'. " +
                       $"Valid: Choked, SlippedAndFell, StarvedAlone, Died.";
        }

        var ls = entity.Get<LifeStateComponent>();
        ls.State              = LifeState.Deceased;
        ls.LastTransitionTick = ctx.Host.TickCount;
        ls.PendingDeathCause  = cause;
        entity.Add(ls);

        string name = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : entity.Id.ToString();

        return $"'{name}' is now Deceased (cause: {cause}).";
    }
}
#endif
