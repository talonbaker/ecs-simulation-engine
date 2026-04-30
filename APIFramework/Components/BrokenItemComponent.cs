namespace APIFramework.Components;

/// <summary>How an item was broken.</summary>
public enum BreakageKind
{
    /// <summary>Broken by being smashed (e.g. thrown against a wall).</summary>
    Smashed,
    /// <summary>Broken by an accidental drop.</summary>
    Dropped,
    /// <summary>Broken by external force-impact (e.g. struck by another object).</summary>
    ForceImpact,
    /// <summary>Cause of breakage unknown or unrecorded.</summary>
    Unknown
}

/// <summary>
/// Marks a persistent broken-item entity spawned by <c>PhysicalManifestSpawner</c>.
/// Paired with a <see cref="ChronicleEntry"/> via <see cref="ChronicleEntryId"/>.
/// </summary>
public struct BrokenItemComponent
{
    /// <summary>Description of the original item (e.g. "coffee-mug", "laptop").</summary>
    public string       OriginalKind     { get; set; }

    /// <summary>How the item was broken.</summary>
    public BreakageKind Breakage         { get; set; }
    /// <summary>SimulationClock.CurrentTick at which the entity was spawned.</summary>
    public long         CreatedAtTick    { get; set; }

    /// <summary>Id of the <c>ChronicleEntry</c> that spawned this entity. Used by invariant check.</summary>
    public string       ChronicleEntryId { get; set; }
}
