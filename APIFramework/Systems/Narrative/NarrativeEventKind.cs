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
    /// <summary>An NPC died from choking. Emitted by LifeStateTransitionSystem when transitioning to Deceased with CauseOfDeath.Choked.</summary>
    Choked,
    /// <summary>An NPC died from a slip-and-fall hazard. Emitted by LifeStateTransitionSystem when transitioning to Deceased with CauseOfDeath.SlippedAndFell.</summary>
    SlippedAndFell,
    /// <summary>An NPC died of starvation while isolated. Emitted by LifeStateTransitionSystem when transitioning to Deceased with CauseOfDeath.StarvedAlone.</summary>
    StarvedAlone,
    /// <summary>Generic death event used by LifeStateTransitionSystem when the CauseOfDeath has no specific narrative kind mapped.</summary>
    Died,
    /// <summary>An NPC began choking (incapacitation has just started). Emitted by ChokingDetectionSystem before the LifeStateTransition request is enqueued, so subscribers see the choker still flagged Alive.</summary>
    ChokeStarted,
    /// <summary>An NPC fainted from extreme fear. Emitted by FaintingDetectionSystem before the incapacitation request is enqueued, so subscribers see the NPC still flagged Alive.</summary>
    Fainted,
    /// <summary>A fainted NPC regained consciousness and returned to Alive state. Emitted by FaintingRecoverySystem before the state transition is applied.</summary>
    RegainedConsciousness,
    /// <summary>An NPC experienced bereavement (learned of or witnessed the death of someone with whom they had a relationship). Emitted by BereavementSystem to record grief impact.</summary>
    BereavementImpact,
    /// <summary>A rescuer successfully intervened and brought an Incapacitated NPC back to Alive. Persistent. Emitted by RescueExecutionSystem after state transition completes.</summary>
    RescuePerformed,
    /// <summary>A rescuer attempted an intervention but it did not succeed. Non-persistent. Emitted by RescueExecutionSystem on a failed roll.</summary>
    RescueAttempted,
    /// <summary>A rescuer attempted an intervention but the victim still died. Persistent — the rescuer carries this. Emitted by RescueExecutionSystem when roll fails and victim proceeds to Deceased.</summary>
    RescueFailed,

    // ── Chore rotation events (WP-3.2.3) ─────────────────────────────────────
    /// <summary>A chore was assigned to an NPC for the day. Emitted by ChoreAssignmentSystem. Not persistent — routine scheduling.</summary>
    ChoreAssigned,
    /// <summary>An NPC completed an assigned chore. Emitted by ChoreExecutionSystem. Not persistent — routine completion.</summary>
    ChoreCompleted,
    /// <summary>An NPC refused a chore assignment (acceptance-bias below threshold). Emitted by ActionSelectionSystem. Persistent.</summary>
    ChoreRefused,
    /// <summary>An NPC completed a chore at low quality. Emitted by ChoreExecutionSystem. Persistent.</summary>
    ChoreBadlyDone,
    /// <summary>An NPC was assigned the same chore beyond the overrotation threshold. Emitted by ChoreExecutionSystem. Persistent.</summary>
    ChoreOverrotation,
}
