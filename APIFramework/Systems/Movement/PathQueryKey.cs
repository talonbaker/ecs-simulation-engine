namespace APIFramework.Systems.Movement;

public readonly record struct PathQueryKey(int FromX, int FromY, int ToX, int ToY, int Seed, long TopologyVersion);
