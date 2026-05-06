#if WARDEN
using APIFramework.Components;

/// <summary>
/// scenario rescue &lt;victim-name&gt; [--rescuer &lt;name&gt;]
/// Triggers a rescue intent from the named rescuer (or nearest alive bystander) toward the victim.
/// </summary>
public sealed class RescueSubverb : IScenarioSubverb
{
    public string Name        => "rescue";
    public string Usage       => "scenario rescue <victim-name|id> [--rescuer <name|id>]";
    public string Description => "Trigger a rescue intent toward an Incapacitated NPC.";

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (ctx.Host?.Engine == null)
            return "ERROR: Engine not available.";

        var victim = ScenarioArgParser.FindEntity(args[0], ctx.Host);
        if (victim == null)
            return $"ERROR: Victim '{args[0]}' not found.";

        if (!victim.Has<LifeStateComponent>())
            return $"ERROR: '{args[0]}' has no LifeStateComponent.";

        if (victim.Get<LifeStateComponent>().State != LifeState.Incapacitated)
            return $"ERROR: '{args[0]}' is not Incapacitated; cannot be rescued.";

        // Find rescuer: explicit --rescuer arg or nearest alive bystander.
        APIFramework.Core.Entity rescuer = null;
        string rescuerArg = ScenarioArgParser.ParseFlagValue(args, "--rescuer");
        if (rescuerArg != null)
        {
            rescuer = ScenarioArgParser.FindEntity(rescuerArg, ctx.Host);
            if (rescuer == null)
                return $"ERROR: Rescuer '{rescuerArg}' not found.";
            if (!rescuer.Has<LifeStateComponent>() ||
                rescuer.Get<LifeStateComponent>().State != LifeState.Alive)
                return $"ERROR: Rescuer '{rescuerArg}' is not Alive.";
        }
        else
        {
            rescuer = FindNearestAlive(victim, ctx.Host);
            if (rescuer == null)
                return "ERROR: No alive bystander found to act as rescuer.";
        }

        // Inject rescue intent directly (same pattern as RescueIntentSystem).
        int victimIntId = ScenarioArgParser.EntityIntId(victim);
        rescuer.Add(new IntendedActionComponent(
            Kind:           IntendedActionKind.Rescue,
            TargetEntityId: victimIntId,
            Context:        DialogContextValue.None,
            IntensityHint:  80
        ));
        rescuer.Add(new MovementTargetComponent
        {
            TargetEntityId = victim.Id,
            Label          = "rescue",
        });

        string rescuerName = rescuer.Has<IdentityComponent>()
            ? rescuer.Get<IdentityComponent>().Name : rescuer.Id.ToString();
        string victimName  = victim.Has<IdentityComponent>()
            ? victim.Get<IdentityComponent>().Name  : victim.Id.ToString();

        return $"'{rescuerName}' is moving to rescue '{victimName}'.";
    }

    private static APIFramework.Core.Entity FindNearestAlive(
        APIFramework.Core.Entity victim, EngineHost host)
    {
        APIFramework.Core.Entity nearest = null;
        float bestDist = float.MaxValue;

        PositionComponent victimPos = victim.Has<PositionComponent>()
            ? victim.Get<PositionComponent>()
            : default;

        foreach (var e in host.Engine.Entities)
        {
            if (e.Id == victim.Id) continue;
            if (!e.Has<NpcTag>()) continue;
            if (!e.Has<LifeStateComponent>()) continue;
            if (e.Get<LifeStateComponent>().State != LifeState.Alive) continue;
            if (!e.Has<PositionComponent>()) continue;

            float d = victimPos.DistanceTo(e.Get<PositionComponent>());
            if (d < bestDist) { bestDist = d; nearest = e; }
        }
        return nearest;
    }
}
#endif
