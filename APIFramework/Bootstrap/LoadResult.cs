namespace APIFramework.Bootstrap;

/// <summary>
/// Summary of what <see cref="WorldDefinitionLoader"/> instantiated during a boot.
/// Returned so callers (bootstrapper, tests) can assert entity counts without querying
/// the entity manager.
/// </summary>
public sealed class LoadResult
{
    public int RoomCount       { get; }
    public int LightSourceCount { get; }
    public int ApertureCount   { get; }
    public int NpcSlotCount    { get; }
    public int ObjectCount     { get; }
    public int SeedUsed        { get; }

    public LoadResult(int rooms, int lightSources, int apertures, int npcSlots, int objects, int seed)
    {
        RoomCount        = rooms;
        LightSourceCount = lightSources;
        ApertureCount    = apertures;
        NpcSlotCount     = npcSlots;
        ObjectCount      = objects;
        SeedUsed         = seed;
    }
}
