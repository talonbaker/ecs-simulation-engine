#if WARDEN
// SpawnCommand.cs
// Spawns a new human NPC at an explicit world position using EntityTemplates.SpawnHuman.
//
// EntityTemplates.SpawnHuman handles:
//   - Creating the entity in the EntityManager
//   - Attaching IdentityComponent (with the given name)
//   - Attaching PositionComponent at (spawnX, 0, spawnZ)
//   - Attaching LifeStateComponent (Alive, zero budget)
//   - Attaching HumanTag
//   - Any other default components defined in the template
//
// Usage:
//   spawn <name> <x> <z>
//
// Example:
//   spawn donna 10.5 3.0
//
// Return conventions:
//   Plain string with entity Guid on success.
//   "ERROR: ..."  on failure.

using APIFramework.Core;

public sealed class SpawnCommand : IDevConsoleCommand
{
    public string Name        => "spawn";
    public string Usage       => "spawn <name> <x> <z>";
    public string Description => "Spawn a new NPC at the given position.";
    public string[] Aliases   => System.Array.Empty<string>();

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length < 3)
            return "ERROR: Usage: " + Usage;

        if (ctx.Host?.Engine == null)
            return "ERROR: Engine not available.";

        // Parse position. Accept both dot and locale-specific decimal separators via
        // InvariantCulture so "10.5" works regardless of the machine's region settings.
        if (!float.TryParse(args[1],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float x))
            return $"ERROR: Invalid x '{args[1]}'. Must be a number.";

        if (!float.TryParse(args[2],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float z))
            return $"ERROR: Invalid z '{args[2]}'. Must be a number.";

        string name   = args[0];
        var    entity = EntityTemplates.SpawnHuman(
            ctx.Host.Engine,
            cfg:    null,
            spawnX: x,
            spawnZ: z,
            name:   name);

        return $"Spawned '{name}' at ({x:F1}, {z:F1}) as entity {entity.Id}.";
    }
}
#endif
