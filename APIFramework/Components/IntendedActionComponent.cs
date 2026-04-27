namespace APIFramework.Components;

public enum IntendedActionKind
{
    Idle,
    Dialog,
    Approach,
    Avoid,
    Linger,
    Work
}

public enum DialogContextValue
{
    None,
    LashOut,
    Share,
    Flirt,
    Deflect,
    BrushOff,
    Acknowledge,
    Greet,
    Refuse,
    Agree,
    Complain,
    Encourage,
    Thanks,
    Apologise
}

/// <summary>
/// Written by ActionSelectionSystem each tick. Overwrites any prior value.
/// Downstream systems read the latest snapshot; there is no queue or history.
/// </summary>
public readonly record struct IntendedActionComponent(
    IntendedActionKind Kind,
    int                TargetEntityId,  // lower-32-bit entity id; 0 when not applicable
    DialogContextValue Context,          // None when Kind != Dialog
    int                IntensityHint     // 0–100 soft modulator for consumers
);
