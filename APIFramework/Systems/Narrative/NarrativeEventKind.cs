namespace APIFramework.Systems.Narrative;

/// <summary>
/// Discriminator for narrative event candidates.
/// Serializes as camelCase strings via JsonOptions.Wire (e.g. "driveSpike").
/// </summary>
public enum NarrativeEventKind
{
    /// <summary>A social drive's Current value moved by at least NarrativeConfig.DriveSpikeThreshold in a single tick.</summary>
    DriveSpike,
    /// <summary>WillpowerComponent.Current dropped by at least NarrativeConfig.WillpowerDropThreshold in a single tick.</summary>
    WillpowerCollapse,
    /// <summary>WillpowerComponent.Current crossed below NarrativeConfig.WillpowerLowThreshold for the first time since being above it.</summary>
    WillpowerLow,
    /// <summary>Two NPCs entered each other's conversation range. Emitted by NarrativeEventDetector from ProximityEnteredConversationRange events.</summary>
    ConversationStarted,
    /// <summary>An NPC left a room shortly after a DriveSpike (within NarrativeConfig.AbruptDepartureWindowTicks).</summary>
    LeftRoomAbruptly,
    /// <summary>An NPC's social mask cracked. Emitted by MaskCrackSystem in the Cleanup phase.</summary>
    MaskSlip,
    /// <summary>A workload task passed its deadline without completion. Emitted by WorkloadSystem.</summary>
    OverdueTask,
    /// <summary>A workload task was completed. Emitted by WorkloadSystem.</summary>
    TaskCompleted,

    // ── Phase 3 — death and incapacitation events (WP-3.0.0) ─────────────────
    /// <summary>An NPC has begun choking. Emitted by ChokingDetectionSystem (WP-3.0.1) before incapacitation.</summary>
    Choked,
    /// <summary>An NPC slipped and fell. Emitted by SlipAndFallSystem (WP-3.0.3).</summary>
    SlippedAndFell,
    /// <summary>An NPC starved in a locked room with no exit. Emitted by StarvationSystem (WP-3.0.3).</summary>
    StarvedAlone,
    /// <summary>
    /// An NPC's LifeState transitioned to Deceased (any cause).
    /// Emitted by LifeStateTransitionSystem immediately before the state flip,
    /// so MemoryRecordingSystem sees Alive participants.
    /// </summary>
    Died,

    // ── Phase 3 — scenario-level events (WP-3.0.1) ───────────────────────────
    /// <summary>
    /// An NPC has begun to choke on food. Emitted by ChokingDetectionSystem at the
    /// instant of choke (before the Incapacitated transition request is enqueued).
    /// Witnesses who record this event remember the start of the episode, not just
    /// the death (which arrives later as NarrativeEventKind.Choked).
    /// </summary>
    ChokeStarted,

    // ── Phase 3 — bereavement events (WP-3.0.2) ──────────────────────────────
    /// <summary>
    /// A non-witness colleague has been impacted by the death of another NPC with
    /// whom they had a meaningful relationship. Emitted by BereavementSystem for each
    /// affected colleague at the moment the death event propagates.
    /// Participants: [colleague.EntityIntId, deceased.EntityIntId].
    /// </summary>
    BereavementImpact,

    // ── Phase 3 — fainting events (WP-3.0.6) ─────────────────────────────────
    /// <summary>
    /// An NPC has fainted from extreme fear (MoodComponent.Fear >= FearThreshold).
    /// Emitted by FaintingDetectionSystem immediately before the Incapacitated transition
    /// request is enqueued — so MemoryRecordingSystem sees an Alive participant.
    /// The NPC will recover automatically after FaintingConfig.FaintDurationTicks.
    /// Persistent: witnesses remember seeing someone faint.
    /// Participants: [faintedNpc.EntityIntId] — witness appended if one is in range.
    /// </summary>
    Fainted,

    /// <summary>
    /// An NPC has regained consciousness after a faint and returned to Alive.
    /// Emitted by FaintingRecoverySystem before the Alive transition request is enqueued.
    /// Not persistent by default — waking up is not typically a memorable standalone event.
    /// Participants: [recoveredNpc.EntityIntId].
    /// </summary>
    RegainedConsciousness,
}
