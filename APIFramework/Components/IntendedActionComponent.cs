namespace APIFramework.Components;

/// <summary>High-level kind of action chosen by ActionSelectionSystem this tick.</summary>
public enum IntendedActionKind
{
    /// <summary>No specific action — the NPC is idling.</summary>
    Idle,
    /// <summary>NPC is engaged in a dialog exchange.</summary>
    Dialog,
    /// <summary>NPC is moving toward another entity.</summary>
    Approach,
    /// <summary>NPC is moving away from another entity.</summary>
    Avoid,
    /// <summary>NPC is staying in place near a target.</summary>
    Linger,
    /// <summary>NPC is performing scheduled work.</summary>
    Work,
    /// <summary>NPC is moving to rescue an Incapacitated NPC (Heimlich, CPR, or door-unlock).</summary>
    Rescue,
    /// <summary>NPC is performing an assigned office chore.</summary>
    ChoreWork,
    /// <summary>NPC is consuming food.</summary>
    Eat,
    /// <summary>NPC is consuming a liquid.</summary>
    Drink,
    /// <summary>NPC is defecating (no accessible bathroom; stays at desk).</summary>
    Defecate,
}

/// <summary>Conversational context associated with an <see cref="IntendedActionKind.Dialog"/> action.</summary>
public enum DialogContextValue
{
    /// <summary>No dialog context (e.g. the action is not a dialog).</summary>
    None,
    /// <summary>NPC lashes out angrily.</summary>
    LashOut,
    /// <summary>NPC shares something personal.</summary>
    Share,
    /// <summary>NPC is flirting.</summary>
    Flirt,
    /// <summary>NPC deflects the topic.</summary>
    Deflect,
    /// <summary>NPC brushes off the partner.</summary>
    BrushOff,
    /// <summary>NPC acknowledges what was said without committing.</summary>
    Acknowledge,
    /// <summary>NPC greets the partner.</summary>
    Greet,
    /// <summary>NPC refuses a request.</summary>
    Refuse,
    /// <summary>NPC agrees to a request.</summary>
    Agree,
    /// <summary>NPC complains.</summary>
    Complain,
    /// <summary>NPC encourages the partner.</summary>
    Encourage,
    /// <summary>NPC expresses thanks.</summary>
    Thanks,
    /// <summary>NPC apologises.</summary>
    Apologise,
    /// <summary>Social mask slips and a hidden feeling leaks through.</summary>
    MaskSlip
}

/// <summary>
/// Written by ActionSelectionSystem each tick. Overwrites any prior value.
/// Downstream systems read the latest snapshot; there is no queue or history.
/// </summary>
/// <param name="Kind">High-level action kind chosen this tick.</param>
/// <param name="TargetEntityId">Lower-32-bit entity id of the action target; 0 when not applicable.</param>
/// <param name="Context">Dialog context. <see cref="DialogContextValue.None"/> when Kind != Dialog.</param>
/// <param name="IntensityHint">Soft modulator in [0, 100] consumed by downstream systems.</param>
public readonly record struct IntendedActionComponent(
    IntendedActionKind Kind,
    int                TargetEntityId,  // lower-32-bit entity id; 0 when not applicable
    DialogContextValue Context,          // None when Kind != Dialog
    int                IntensityHint     // 0–100 soft modulator for consumers
);
