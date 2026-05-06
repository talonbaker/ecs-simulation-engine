#if WARDEN
// MoveCommand.cs
// Teleports an entity to a new (X, Z) world position via IWorldMutationApi.MoveEntity.
//
// Why go through MutationApi rather than directly patching PositionComponent?
//   MoveEntity enforces engine-side invariants: it validates that the entity carries a
//   MutableTopologyTag (which marks entities allowed to change rooms at runtime), and
//   it fires the room-change event that the occupancy / pathfinding / sensor systems
//   listen to. A raw component write would silently desync those systems.
//
// Y is always set to 0 (ground plane). Pass a different value if you extend this command
// to support vertical movement (e.g. multi-floor buildings).
//
// Usage:
//   move <entityId|name> <x> <z>
//
// Example:
//   move donna 12.0 4.5
//
// Return conventions:
//   Plain string on success.
//   "ERROR: ..."  on failure (including MoveEntity returning false).

using APIFramework.Components;

public sealed class MoveCommand : IDevConsoleCommand
{
    public string Name        => "move";
    public string Usage       => "move <entityId|name> <x> <z>";
    public string Description => "Move entity to world position via MutationApi.MoveEntity.";
    public string[] Aliases   => System.Array.Empty<string>();

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length < 3)
            return "ERROR: Usage: " + Usage;

        if (ctx.Host?.Engine == null)
            return "ERROR: Engine not available.";

        if (ctx.MutationApi == null)
            return "ERROR: MutationApi not available.";

        var entity = FindEntity(args[0], ctx.Host);
        if (entity == null)
            return $"ERROR: Entity '{args[0]}' not found.";

        if (!float.TryParse(args[1],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float x))
            return $"ERROR: Invalid x '{args[1]}'.";

        if (!float.TryParse(args[2],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float z))
            return $"ERROR: Invalid z '{args[2]}'.";

        // IWorldMutationApi.MoveEntity now takes int tile coordinates and
        // returns void; throws on invalid id / missing MutableTopologyTag /
        // out-of-grid position. Dispatcher catches exceptions and converts
        // them to ERROR output for us.
        int tileX = UnityEngine.Mathf.RoundToInt(x);
        int tileY = UnityEngine.Mathf.RoundToInt(z);
        ctx.MutationApi.MoveEntity(entity.Id, tileX, tileY);

        return $"Moved '{args[0]}' to tile ({tileX}, {tileY}).";
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
