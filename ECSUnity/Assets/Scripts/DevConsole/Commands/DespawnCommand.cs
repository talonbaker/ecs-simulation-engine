#if WARDEN
// DespawnCommand.cs
// Immediately removes an entity from the engine via EntityManager.DestroyEntity.
//
// This is a hard removal — it bypasses any death/cleanup systems (LifeStateTransitionSystem,
// corpse spawning, inventory drop, etc.). Use force-kill if you want the simulation's
// death pipeline to run. Use despawn only when you need to surgically remove an entity
// without side-effects (e.g., debug cleanup, removing a broken entity mid-session).
//
// Usage:
//   despawn <entityId|name>
//
// Example:
//   despawn donna
//   despawn 3f2504e0-4f89-11d3-9a0c-0305e82c3301
//
// Return conventions:
//   Plain string on success.
//   "ERROR: ..."  on failure.

using APIFramework.Components;

public sealed class DespawnCommand : IDevConsoleCommand
{
    public string Name        => "despawn";
    public string Usage       => "despawn <entityId|name>";
    public string Description => "Despawn and remove an entity from the world.";
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

        // Capture display name before destruction.
        string displayName = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : entity.Id.ToString();

        // Hard removal — no simulation death pipeline.
        ctx.Host.Engine.DestroyEntity(entity);

        return $"Despawned '{displayName}' ({entity.Id}).";
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
