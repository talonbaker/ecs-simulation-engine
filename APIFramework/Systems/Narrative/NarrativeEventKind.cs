namespace APIFramework.Systems.Narrative;

/// <summary>
/// Discriminator for narrative event candidates.
/// Serializes as camelCase strings via JsonOptions.Wire (e.g. "driveSpike").
/// </summary>
public enum NarrativeEventKind
{
    /// <summary>A drive value crossed an authored spike threshold within a tick.</summary>
    DriveSpike,
    /// <summary>An NPC's willpower dropped to zero (or to the configured collapse floor), preventing further volitional action.</summary>
    WillpowerCollapse,
    /// <summary>An NPC's willpower fell below the "low" threshold but has not yet collapsed.</summary>
    WillpowerLow,
    /// <summary>Two NPCs began a conversation (entered conversation range with intent to dialog).</summary>
    ConversationStarted,
    /// <summary>An NPC exited a room without completing the action they were performing in it.</summary>
    LeftRoomAbruptly,
    /// <summary>An NPC's social mask cracked, leaking authentic mood/intent through the mask layer.</summary>
    MaskSlip,
    /// <summary>A workload task passed its DeadlineTick without being completed.</summary>
    OverdueTask,
    /// <summary>A workload task was completed (progress reached 100%).</summary>
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
