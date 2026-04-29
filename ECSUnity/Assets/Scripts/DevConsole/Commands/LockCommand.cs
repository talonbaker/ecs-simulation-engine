#if WARDEN
// LockCommand.cs
// Attaches an obstacle marker (LockedTag) to a door entity via IWorldMutationApi.AttachObstacle.
//
// AttachObstacle tells the topology/pathfinding system to treat the entity as impassable.
// The exact tag type is managed internally by MutationApi — from this command's perspective,
// we simply declare that the door should block passage.
//
// Note: This command requires a Guid argument because doors are structural entities and do
// not carry a human-readable IdentityComponent.Name in the current data model. If that
// changes, extend FindEntity here to support name lookup.
//
// Usage:
//   lock <doorId>
//
// Example:
//   lock 3f2504e0-4f89-11d3-9a0c-0305e82c3301
//
// Return conventions:
//   Plain string on success.
//   "ERROR: ..."  on failure.

public sealed class LockCommand : IDevConsoleCommand
{
    public string Name        => "lock";
    public string Usage       => "lock <doorId>";
    public string Description => "Attach LockedTag to a door via MutationApi.AttachObstacle.";
    public string[] Aliases   => System.Array.Empty<string>();

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (ctx.MutationApi == null)
            return "ERROR: MutationApi not available.";

        // Doors are identified by Guid only.
        if (!System.Guid.TryParse(args[0], out var doorId))
            return $"ERROR: '{args[0]}' is not a valid entity GUID.";

        bool ok = ctx.MutationApi.AttachObstacle(doorId);

        return ok
            ? $"Locked door {doorId}."
            : $"ERROR: AttachObstacle returned false for {doorId}. " +
              $"Entity may not exist or already has an obstacle marker.";
    }
}
#endif
