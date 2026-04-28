namespace APIFramework.Components;

/// <summary>
/// The three states of life for an NPC entity.
/// Only <see cref="LifeStateTransitionSystem"/> is permitted to write this value.
/// </summary>
public enum LifeState
{
    /// <summary>Normal simulation participation. All cognitive and physiology systems tick.</summary>
    Alive = 0,

    /// <summary>
    /// Non-volitional but biologically alive. Cognition and social systems skip this entity;
    /// physiology systems continue (digestion, energy drain, bladder fill).
    /// <see cref="LifeStateComponent.IncapacitatedTickBudget"/> counts down each tick;
    /// when it reaches zero, <see cref="LifeStateTransitionSystem"/> transitions to
    /// <see cref="Deceased"/> using <see cref="LifeStateComponent.PendingDeathCause"/>.
    /// </summary>
    Incapacitated = 1,

    /// <summary>
    /// Permanent. All systems skip this entity. The entity id, relationship rows,
    /// and memory entries that reference it remain valid — the engine does not erase
    /// a person from history. <see cref="CauseOfDeathComponent"/> is attached.
    /// </summary>
    Deceased = 2,
}

/// <summary>
/// The specific cause that will (or did) cause death.
/// Mirrors the death-related <see cref="APIFramework.Systems.Narrative.NarrativeEventKind"/> values.
/// </summary>
public enum CauseOfDeath
{
    /// <summary>No cause recorded. Valid only while <see cref="LifeState.Alive"/>.</summary>
    Unknown = 0,
    Choked = 1,
    SlippedAndFell = 2,
    StarvedAlone = 3,
}

/// <summary>
/// Per-NPC life-state tracker. Attached to every NPC by
/// <see cref="APIFramework.Systems.LifeState.LifeStateInitializerSystem"/> at world boot.
/// <para>
/// Only <see cref="APIFramework.Systems.LifeState.LifeStateTransitionSystem"/>
/// may write <see cref="State"/>. All other systems must treat it as read-only.
/// </para>
/// </summary>
public struct LifeStateComponent
{
    /// <summary>Current life state. Default: <see cref="LifeState.Alive"/>.</summary>
    public LifeState State;

    /// <summary>
    /// SimulationClock.CurrentTick at the most recent state change.
    /// Zero for entities that have been Alive since spawn.
    /// </summary>
    public long LastTransitionTick;

    /// <summary>
    /// When <see cref="State"/> is <see cref="LifeState.Incapacitated"/>, decremented
    /// exactly once per tick by <see cref="APIFramework.Systems.LifeState.LifeStateTransitionSystem"/>.
    /// When it reaches zero, the entity transitions to <see cref="LifeState.Deceased"/>
    /// with cause <see cref="PendingDeathCause"/>.
    /// Zero while <see cref="LifeState.Alive"/> or <see cref="LifeState.Deceased"/>.
    /// </summary>
    public int IncapacitatedTickBudget;

    /// <summary>
    /// Cause to record when the <see cref="IncapacitatedTickBudget"/> expires.
    /// Set by <see cref="APIFramework.Systems.LifeState.LifeStateTransitionSystem"/>
    /// at the moment of Incapacitation. Unknown while Alive or Deceased.
    /// </summary>
    public CauseOfDeath PendingDeathCause;
}

/// <summary>
/// Attached to an NPC entity the moment <see cref="LifeState.Deceased"/> is entered.
/// Records the forensic context of the death for memory, chronicle, and bereavement systems.
/// </summary>
public struct CauseOfDeathComponent
{
    /// <summary>What killed this NPC.</summary>
    public CauseOfDeath Cause;

    /// <summary>SimulationClock.CurrentTick at the moment of transition to Deceased.</summary>
    public long DeathTick;

    /// <summary>
    /// Entity id of the first Alive NPC in conversation range at the moment of death.
    /// <see cref="Guid.Empty"/> if the death was unwitnessed.
    /// </summary>
    public Guid WitnessedByNpcId;

    /// <summary>
    /// The string UUID of the room (from <see cref="RoomComponent.Id"/>) the NPC occupied at death,
    /// as resolved via <c>EntityRoomMembership</c> at the moment of transition to Deceased.
    /// Null if the NPC was not in a tracked room.
    /// Preserved for bereavement (WP-3.0.2) and slip-on-stain queries (WP-3.0.3).
    /// </summary>
    public string? LocationRoomId;
}
