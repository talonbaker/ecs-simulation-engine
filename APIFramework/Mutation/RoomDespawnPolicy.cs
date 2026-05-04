namespace APIFramework.Mutation;

/// <summary>
/// How <see cref="IWorldMutationApi.DespawnRoom"/> handles entities that reference
/// the room being deleted (lights, apertures, NPC slots, anchor objects with matching RoomId).
/// </summary>
public enum RoomDespawnPolicy
{
    /// <summary>
    /// Delete only the room entity. Contents (lights, NPCs, anchors) are left in place
    /// with their <c>RoomId</c> field still pointing at the now-gone room. Useful when the
    /// caller wants to keep the props but rebuild the room around them.
    /// </summary>
    OrphanContents,

    /// <summary>
    /// Delete the room entity AND all entities whose RoomId matches it (lights, apertures,
    /// anchor objects). NPC slots are NOT cascade-deleted (NPCs persist as a deliberate
    /// invariant — losing a room should not delete its occupants).
    /// </summary>
    CascadeDelete,
}
