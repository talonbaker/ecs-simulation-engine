#if WARDEN
using APIFramework.Components;
using APIFramework.Systems.Physics;

/// <summary>
/// scenario seed-stains &lt;count&gt;
/// Spawns N slip-hazard stains at random walkable tiles.
/// </summary>
public sealed class SeedStainsSubverb : IScenarioSubverb
{
    public string Name        => "seed-stains";
    public string Usage       => "scenario seed-stains <count>";
    public string Description => "Spawn N slip-hazard stains at random walkable tiles.";

    private const int MaxStains = 100;

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (!int.TryParse(args[0], out int count) || count < 1)
            return $"ERROR: '{args[0]}' is not a valid positive integer.";

        if (count > MaxStains)
            return $"ERROR: Count capped at {MaxStains}.";

        if (ctx.Host?.Engine == null)
            return "ERROR: Engine not available.";

        if (ctx.MutationApi == null)
            return "ERROR: MutationApi not available.";

        // Collect walkable tile positions from NPC positions as a reference pool.
        var tiles = CollectWalkableTiles(ctx.Host);
        if (tiles.Count == 0)
            return "ERROR: No walkable tiles found (no entities with PositionComponent).";

        var rng = new System.Random((int)(ctx.Host.TickCount & 0x7FFFFFFF));
        for (int i = 0; i < count; i++)
        {
            int idx   = rng.Next(tiles.Count);
            var (tx, tz) = tiles[idx];
            ctx.MutationApi.SpawnStain(StainTemplates.WaterPuddle, tx, tz);
        }

        return $"Spawned {count} stain(s) at random walkable tiles.";
    }

    private static System.Collections.Generic.List<(int, int)> CollectWalkableTiles(EngineHost host)
    {
        var set = new System.Collections.Generic.HashSet<(int, int)>();
        foreach (var e in host.Engine.Entities)
        {
            if (!e.Has<PositionComponent>()) continue;
            if (e.Has<ObstacleTag>() || e.Has<StructuralTag>()) continue;
            var p = e.Get<PositionComponent>();
            set.Add(((int)p.X, (int)p.Z));
        }
        return new System.Collections.Generic.List<(int, int)>(set);
    }
}
#endif
