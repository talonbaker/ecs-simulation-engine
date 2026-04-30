namespace APIFramework.Components;

/// <summary>
/// Marker data on an NPC-slot entity. The cast generator (WP-1.8.A) reads these slots
/// and replaces them with fully-spawned NPC entities.
/// </summary>
public struct NpcSlotComponent
{
    /// <summary>Tile X coordinate of the NPC spawn slot.</summary>
    public int     X             { get; init; }
    /// <summary>Tile Y coordinate of the NPC spawn slot.</summary>
    public int     Y             { get; init; }
    /// <summary>Optional archetype hint to constrain which NPC archetype the cast generator picks.</summary>
    public string? ArchetypeHint { get; init; }
    /// <summary>Id of the <see cref="RoomComponent"/> the slot lives in, when known at authoring time.</summary>
    public string? RoomId        { get; init; }
}
