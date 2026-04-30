namespace APIFramework.Bootstrap;

/// <summary>
/// Summary of what <see cref="WorldDefinitionLoader"/> instantiated during a boot.
/// Returned so callers (bootstrapper, tests) can assert entity counts without querying
/// the entity manager.
/// </summary>
public sealed class LoadResult
{
    /// <summary>Number of room entities created from the world definition.</summary>
    public int RoomCount       { get; }

    /// <summary>Number of light-source entities created from the world definition.</summary>
    public int LightSourceCount { get; }

    /// <summary>Number of light-aperture entities created from the world definition.</summary>
    public int ApertureCount   { get; }

    /// <summary>Number of NPC slot marker entities created (consumed later by <see cref="CastGenerator"/>).</summary>
    public int NpcSlotCount    { get; }

    /// <summary>Number of anchor-object entities created from the world definition.</summary>
    public int ObjectCount     { get; }

    /// <summary>Random seed read from the world-definition JSON and used for deterministic spawning.</summary>
    public int SeedUsed        { get; }

    /// <summary>Creates a result snapshot of the entity counts and seed produced during a load.</summary>
    /// <param name="rooms">Number of room entities created.</param>
    /// <param name="lightSources">Number of light-source entities created.</param>
    /// <param name="apertures">Number of light-aperture entities created.</param>
    /// <param name="npcSlots">Number of NPC slot marker entities created.</param>
    /// <param name="objects">Number of anchor-object entities created.</param>
    /// <param name="seed">Seed value pulled from the world definition.</param>
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
