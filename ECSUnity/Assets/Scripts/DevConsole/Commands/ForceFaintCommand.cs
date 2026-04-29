#if WARDEN
// ForceFaintCommand.cs
// Forces an NPC into the Incapacitated (fainted) state, which is the non-terminal
// middle state between Alive and Deceased.
//
// How recovery works:
//   Each tick, LifeStateTransitionSystem decrements IncapacitatedTickBudget.
//   If the budget reaches zero and no recovery condition is met, the entity dies
//   (Deceased). If a rescuer intervenes (system-dependent), the budget resets and
//   the entity transitions back to Alive.
//
//   DefaultIncapacitatedBudget = 600 ticks. At 50 tps this is 12 real-seconds (game
//   time may differ if time scale is not 1). Adjust the constant if you want a
//   longer/shorter recovery window in testing.
//
// Preconditions:
//   - Entity must have LifeStateComponent.
//   - Entity must not already be Deceased (fainting a corpse is an error).
//
// Usage:
//   force-faint <npcId|name>
//
// Example:
//   force-faint donna
//
// Return conventions:
//   Plain string on success.
//   "ERROR: ..."  on failure.

using APIFramework.Components;

public sealed class ForceFaintCommand : IDevConsoleCommand
{
    public string Name        => "force-faint";
    public string Usage       => "force-faint <npcId|name>";
    public string Description => "Force an NPC to Incapacitated (fainting recovery path).";
    public string[] Aliases   => System.Array.Empty<string>();

    // Generous budget — long enough for a human to notice and rescue in manual testing.
    // 600 ticks @ 50 tps = 12 seconds real-time (at 1x time scale).
    private const int DefaultIncapacitatedBudget = 600;

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
            return $"ERROR: Entity '{args[0]}' has no LifeStateComponent.";

        var ls = entity.Get<LifeStateComponent>();

        // Guard: cannot faint an already-dead entity.
        if (ls.State == LifeState.Deceased)
            return $"ERROR: '{args[0]}' is already Deceased; cannot transition to Incapacitated.";

        // Mutate. entity.Add<T> replaces the existing component.
        ls.State                   = LifeState.Incapacitated;
        ls.LastTransitionTick      = ctx.Host.TickCount;
        ls.IncapacitatedTickBudget = DefaultIncapacitatedBudget;
        ls.PendingDeathCause       = CauseOfDeath.Unknown; // cleared — not dying yet
        entity.Add(ls);

        string name = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : entity.Id.ToString();

        return $"Force-fainted '{name}'. " +
               $"Tick budget: {DefaultIncapacitatedBudget} ticks before Deceased (unless rescued).";
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
