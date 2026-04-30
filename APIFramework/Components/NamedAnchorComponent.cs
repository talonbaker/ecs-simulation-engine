namespace APIFramework.Components;

/// <summary>
/// Added to room entities that correspond to a named anchor from the world bible.
/// Carries the anchor tag, the world-bible description, and optional smell metadata.
/// </summary>
public struct NamedAnchorComponent
{
    /// <summary>Stable anchor tag from the world bible (e.g. "the-coffee-machine").</summary>
    public string  Tag         { get; init; }
    /// <summary>Human-authored description of the anchor.</summary>
    public string  Description { get; init; }
    /// <summary>Optional smell tag for atmosphere systems; null when not authored.</summary>
    public string? SmellTag    { get; init; }
}
