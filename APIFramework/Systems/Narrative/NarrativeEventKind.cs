namespace APIFramework.Systems.Narrative;

/// <summary>
/// Discriminator for narrative event candidates.
/// Serializes as camelCase strings via JsonOptions.Wire (e.g. "driveSpike").
/// </summary>
public enum NarrativeEventKind
{
    DriveSpike,
    WillpowerCollapse,
    WillpowerLow,
    ConversationStarted,
    LeftRoomAbruptly,
    MaskSlip,
    OverdueTask,
    TaskCompleted,
    Choked,
    SlippedAndFell,
    StarvedAlone,
    Died,
    ChokeStarted,
}
