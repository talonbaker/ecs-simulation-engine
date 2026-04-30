using System;
using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>
/// Tracks which corpse entities have already triggered the proximity-bereavement hit on this NPC.
///
/// Used by <see cref="APIFramework.Systems.LifeState.BereavementByProximitySystem"/> to ensure
/// the one-shot "NPC enters the room of a corpse" stress hit fires exactly once per (NPC, corpse)
/// pair, regardless of how long the NPC remains in the room or how many times they re-enter.
///
/// Attached lazily by BereavementByProximitySystem when the NPC first encounters a corpse.
/// Contains a reference type (HashSet) — treat as a persistent record, not a value snapshot.
///
/// WP-3.0.2: Deceased-Entity Handling + Bereavement.
/// </summary>
public struct BereavementHistoryComponent
{
    /// <summary>
    /// Set of entity Guids (corpse entities) whose proximity-bereavement hit has already
    /// been applied to this NPC. Grows monotonically; never trimmed at v0.1.
    /// </summary>
    public HashSet<Guid> EncounteredCorpseIds;
}
