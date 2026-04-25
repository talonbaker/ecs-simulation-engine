namespace APIFramework.Components;

/// <summary>
/// Marker data on an NPC-slot entity. The cast generator (WP-1.8.A) reads these slots
/// and replaces them with fully-spawned NPC entities.
/// </summary>
public struct NpcSlotComponent
{
    public int     X             { get; init; }
    public int     Y             { get; init; }
    public string? ArchetypeHint { get; init; }
    public string? RoomId        { get; init; }
}
