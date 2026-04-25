namespace APIFramework.Components;

/// <summary>
/// Added to room entities that correspond to a named anchor from the world bible.
/// Carries the anchor tag, the world-bible description, and optional smell metadata.
/// </summary>
public struct NamedAnchorComponent
{
    public string  Tag         { get; init; }
    public string  Description { get; init; }
    public string? SmellTag    { get; init; }
}
