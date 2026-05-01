#if WARDEN
// ForceKillCommand.cs
// Instantly sets an NPC's LifeState to Deceased by directly mutating LifeStateComponent.
//
// This bypasses the normal death pipeline (hunger, injury, hazard systems) and goes
// straight to the terminal state. The LifeStateTransitionSystem will see Deceased on its
// next tick and trigger downstream consequences (corpse spawning, notifications, etc.)
// exactly as it would after a natural death — only the cause-detection step is skipped.
//
// Supported cause values (CauseOfDeath enum):
//   Unknown        — 0 (default)
//   Choked         — 1
//   SlippedAndFell — 2
//   StarvedAlone   — 3
//
// Usage:
//   force-kill <npcId|name> [cause]
//
// Examples:
//   force-kill donna
//   force-kill donna Choked
//   force-kill 3f2504e0-4f89-11d3-9a0c-0305e82c3301 StarvedAlone
//
// Return conventions:
//   Plain string on success.
//   "ERROR: ..."  on failure.

using APIFramework.Components;

public sealed class ForceKillCommand : IDevConsoleCommand
{
    public string Name        => "force-kill";
    public string Usage       => "force-kill <npcId|name> [cause]";
    public string Description => "Force an NPC to Deceased. Cause: Unknown|Choked|SlippedAndFell|StarvedAlone.";
    public string[] Aliases   => System.Array.Empty<string>();

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (ctx.Host?.Engine == null)
            return "ERROR: Engine not available.";

        var entity = FindEntity(args[0], ctx.Host);
        if (entity == null)
            return $"ERROR: Entity '{args[0]}' not found.";

        if (!entity.Has<LifeStateComponent>())
            return $"ERROR: Entity '{args[0]}' has no LifeStateComponent — is it a human NPC?";

        // Parse optional cause argument. Defaults to Unknown.
        CauseOfDeath cause = CauseOfDeath.Unknown;
        if (args.Length > 1)
        {
            if (!System.Enum.TryParse(args[1], ignoreCase: true, out cause))
                return $"ERROR: Unknown cause '{args[1]}'. " +
                       $"Valid values: Unknown, Choked, SlippedAndFell, StarvedAlone.";
        }

        // Read current component, mutate, write back.
        // entity.Add<T> replaces the existing component — this is the correct mutation path.
        var ls = entity.Get<LifeStateComponent>();
        ls.State              = LifeState.Deceased;
        ls.LastTransitionTick = ctx.Host.TickCount;
        ls.PendingDeathCause  = cause;
        entity.Add(ls);

        string name = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : entity.Id.ToString();

        return $"Force-killed '{name}' (cause: {cause}). LifeState is now Deceased.";
    }

    // Tries Guid first, then falls back to case-insensitive IdentityComponent.Name match.
    private static APIFramework.Core.Entity FindEntity(string idOrName, EngineHost host)
    {
        if (host?.Engine?.Entities == null) return null;

        if (System.Guid.TryParse(idOrName, out var guid))
        {
            foreach (var e in host.Engine.Entities)
                if (e.Id == guid) return e;
        }

        string lower = idOrName.ToLowerInvariant();
        foreach (var e in host.Engine.Entities)
        {
            if (e.Has<IdentityComponent>())
            {
                var id = e.Get<IdentityComponent>();
                if (id.Name?.ToLowerInvariant() == lower) return e;
            }
        }

        return null;
    }
}
#endif
