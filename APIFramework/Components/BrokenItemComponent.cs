namespace APIFramework.Components;

/// <summary>How an item was broken.</summary>
public enum BreakageKind
{
    Smashed,
    Dropped,
    ForceImpact,
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

    public BreakageKind Breakage         { get; set; }
    public long         CreatedAtTick    { get; set; }

    /// <summary>Id of the <c>ChronicleEntry</c> that spawned this entity. Used by invariant check.</summary>
    public string       ChronicleEntryId { get; set; }
}
