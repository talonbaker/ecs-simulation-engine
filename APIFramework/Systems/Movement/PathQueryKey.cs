namespace APIFramework.Systems.Movement;

/// <summary>
/// Cache key for pathfinding queries. Includes the topologyVersion so that
/// entries from different topology versions are distinct, allowing lazy eviction.
/// </summary>
public readonly record struct PathQueryKey(
    int FromX,
    int FromY,
    int ToX,
    int ToY,
    int Seed,
    long TopologyVersion
);
