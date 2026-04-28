using System;

namespace APIFramework.Components;

/// <summary>
/// Attached to an NPC entity by <see cref="APIFramework.Systems.LifeState.CorpseSpawnerSystem"/>
/// at the moment a death narrative event is received. Persists for the lifetime of the entity.
///
/// Provides fast read access to death metadata without cross-referencing
/// <see cref="CauseOfDeathComponent"/> in performance-sensitive loops (e.g. proximity checks).
///
/// WP-3.0.2: Deceased-Entity Handling + Bereavement.
/// </summary>
public struct CorpseComponent
{
    /// <summary>SimulationClock.CurrentTick at the moment of death. Mirrors CauseOfDeathComponent.DeathTick.</summary>
    public long DeathTick;

    /// <summary>
    /// The Guid of the deceased entity (the entity carrying this component).
    /// Stored explicitly for clarity in cross-entity queries — a system iterating
    /// CorpseTag entities can pass this to other systems without re-deriving the entity's id.
    /// </summary>
    public Guid OriginalNpcEntityId;

    /// <summary>
    /// Room UUID where the NPC died. Mirrors CauseOfDeathComponent.LocationRoomId.
    /// Null when the NPC died outside all room bounds.
    /// </summary>
    public string? LocationRoomId;

    /// <summary>
    /// False at spawn (the body remains where it fell).
    /// Set to true when the player drags the corpse via IWorldMutationApi.MoveCorpse
    /// (deferred to WP-3.0.4 merge; this field is the substrate for that mechanic).
    /// </summary>
    public bool HasBeenMoved;
}
