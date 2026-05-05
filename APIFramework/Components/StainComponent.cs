namespace APIFramework.Components;

/// <summary>
/// Marks a persistent physical spill entity spawned by <c>PhysicalManifestSpawner</c>.
/// Paired with a <c>ChronicleEntry</c> via <see cref="ChronicleEntryId"/>.
/// </summary>
public struct StainComponent
{
    /// <summary>Description of what caused the stain (e.g. "participant:7").</summary>
    public string Source           { get; set; }

    /// <summary>Visual / gameplay weight; 0 (tiny dot) to 100 (large puddle).</summary>
    public int    Magnitude        { get; set; }

    /// <summary>SimulationClock.CurrentTick at which the stain was spawned.</summary>
    public long   CreatedAtTick    { get; set; }

    /// <summary>Id of the <c>ChronicleEntry</c> that spawned this stain. Used by invariant check.</summary>
    public string ChronicleEntryId { get; set; }
}
