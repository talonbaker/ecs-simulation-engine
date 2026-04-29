#if WARDEN
// UnlockCommand.cs
// Removes the obstacle marker (LockedTag) from a door entity via IWorldMutationApi.DetachObstacle.
//
// This is the inverse of 'lock'. After calling unlock, the pathfinding system will treat
// the door as passable again on its next topology refresh tick.
//
// Note: This command requires a Guid argument because doors are structural entities and do
// not carry a human-readable IdentityComponent.Name in the current data model.
//
// Usage:
//   unlock <doorId>
//
// Example:
//   unlock 3f2504e0-4f89-11d3-9a0c-0305e82c3301
//
// Return conventions:
//   Plain string on success.
//   "ERROR: ..."  on failure.

public sealed class UnlockCommand : IDevConsoleCommand
{
    public string Name        => "unlock";
    public string Usage       => "unlock <doorId>";
    public string Description => "Remove LockedTag from a door via MutationApi.DetachObstacle.";
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

        bool ok = ctx.MutationApi.DetachObstacle(doorId);

        return ok
            ? $"Unlocked door {doorId}."
            : $"ERROR: DetachObstacle returned false for {doorId}. " +
              $"Entity may not exist or does not currently have an obstacle marker.";
    }
}
#endif
