#if WARDEN
using APIFramework.Components;

/// <summary>
/// scenario choke &lt;npc-name|--random&gt; [--bolus-size &lt;small|medium|large&gt;]
/// Attaches ChokingComponent with the specified bolus size; choke timer starts.
/// </summary>
public sealed class ChokeSubverb : IScenarioSubverb
{
    public string Name        => "choke";
    public string Usage       => "scenario choke <npc-name|--random> [--bolus-size <small|medium|large>]";
    public string Description => "Trigger a choke event on an NPC. Rescue window opens.";

    private const int DefaultBudget = 600;

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (ctx.Host?.Engine == null)
            return "ERROR: Engine not available.";

        APIFramework.Core.Entity entity;
        if (ScenarioArgParser.HasFlag(args, "--random"))
        {
            entity = FindRandomAliveNpc(ctx.Host);
            if (entity == null)
                return "ERROR: No alive NPC found to choke.";
        }
        else
        {
            entity = ScenarioArgParser.FindEntity(args[0], ctx.Host);
            if (entity == null)
                return $"ERROR: Entity '{args[0]}' not found.";
        }

        if (!entity.Has<LifeStateComponent>())
            return "ERROR: Entity has no LifeStateComponent — is it a human NPC?";

        var ls = entity.Get<LifeStateComponent>();
        if (ls.State != LifeState.Alive)
            return $"ERROR: NPC is not Alive (state: {ls.State}).";

        float bolusSize = 1.0f;
        string bolusSizeLabel = "medium";
        var bolusSizeStr = ScenarioArgParser.ParseFlagValue(args, "--bolus-size");
        if (bolusSizeStr != null)
        {
            switch (bolusSizeStr.ToLowerInvariant())
            {
                case "small":  bolusSize = 0.5f; bolusSizeLabel = "small";  break;
                case "medium": bolusSize = 1.0f; bolusSizeLabel = "medium"; break;
                case "large":  bolusSize = 2.0f; bolusSizeLabel = "large";  break;
                default:
                    return $"ERROR: Unknown bolus size '{bolusSizeStr}'. Valid: small, medium, large.";
            }
        }

        ls.State                   = LifeState.Incapacitated;
        ls.LastTransitionTick      = ctx.Host.TickCount;
        ls.IncapacitatedTickBudget = DefaultBudget;
        ls.PendingDeathCause       = CauseOfDeath.Choked;
        entity.Add(ls);

        entity.Add(new ChokingComponent
        {
            ChokeStartTick = ctx.Host.TickCount,
            RemainingTicks = DefaultBudget,
            BolusSize      = bolusSize,
            PendingCause   = CauseOfDeath.Choked,
        });

        string name = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : entity.Id.ToString();

        return $"{name} is choking on a {bolusSizeLabel} bolus.";
    }

    private static APIFramework.Core.Entity FindRandomAliveNpc(EngineHost host)
    {
        foreach (var e in host.Engine.Entities)
        {
            if (!e.Has<NpcTag>() || !e.Has<LifeStateComponent>()) continue;
            if (e.Get<LifeStateComponent>().State == LifeState.Alive) return e;
        }
        return null;
    }
}
#endif
