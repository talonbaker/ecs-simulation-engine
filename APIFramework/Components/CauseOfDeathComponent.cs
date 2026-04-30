namespace APIFramework.Components;

/// <summary>
/// Reason for death. Enum values correspond to NarrativeEventKind values in MemoryRecordingSystem.
/// </summary>
public enum CauseOfDeath
{
    Unknown = 0,
    Choked = 1,
    SlippedAndFell = 2,
    StarvedAlone = 3,
    // Future: Killed, Heart, Suicide, etc. Out of scope at v0.1.
}

/// <summary>
/// Attached to a deceased NPC (LifeStateComponent.State == Deceased).
/// Populated by LifeStateTransitionSystem when the NPC transitions to Deceased.
/// Preserved for the lifetime of the entity to maintain death history.
/// </summary>
public struct CauseOfDeathComponent
{
    /// <summary>Why the NPC died.</summary>
    public CauseOfDeath Cause;

    /// <summary>SimulationClock.CurrentTick when the transition to Deceased occurred.</summary>
    public long DeathTick;

    /// <summary>
    /// Entity ID of the NPC who witnessed the death (was in conversation range).
    /// Guid.Empty if unwitnessed.
    /// </summary>
    public Guid WitnessedByNpcId;

    /// <summary>
    /// Room ID (RoomMembershipComponent.RoomId) where the death occurred.
    /// Used by 3.0.2's relationship-shift logic and 3.0.3's stain handling.
    /// </summary>
    public Guid LocationRoomId;
}
