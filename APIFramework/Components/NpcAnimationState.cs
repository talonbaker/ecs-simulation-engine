namespace APIFramework.Components;

/// <summary>
/// Animation states that map engine simulation state to Unity Animator states.
/// Consumed by NpcAnimatorController which sets corresponding Animator bool parameters.
/// Extended in WP-3.2.6 from 7 base states (WP-3.1.B) to 15 total.
/// </summary>
public enum NpcAnimationState
{
    /// <summary>No intent; standing still. Default state.</summary>
    Idle,

    /// <summary>Moving toward a target (IntendedActionKind.Approach, velocity > 0).</summary>
    Walk,

    /// <summary>Seated at a desk or other anchor.</summary>
    Sit,

    /// <summary>In active dialogue (IntendedActionKind.Dialog).</summary>
    Talk,

    /// <summary>Choking OR MoodComponent.PanicLevel >= 0.5. Frozen, facing forward.</summary>
    Panic,

    /// <summary>LifeState == Incapacitated (fainting) or scheduled sleep.</summary>
    Sleep,

    /// <summary>LifeState == Deceased. Static slumped pose; no animation.</summary>
    Dead,

    // ── WP-3.2.6 additions ───────────────────────────────────────────────────

    /// <summary>IntendedAction.Kind == Eat. Arm-to-mouth cycle; emits Chew per cycle.</summary>
    Eating,

    /// <summary>IntendedAction.Kind == Drink. Container-tilt motion; emits Slurp per cycle.</summary>
    Drinking,

    /// <summary>IntendedAction.Kind == Defecate. Awkward seated pose; no sound.</summary>
    DefecatingInCubicle,

    /// <summary>Energy &lt; 25 and not in scheduled sleep. Slumped at desk; chibi SleepZ slot active.</summary>
    SleepingAtDesk,

    /// <summary>IntendedAction.Kind == Work. Keyboard-typing arm motion; emits KeyboardClack per cycle.</summary>
    Working,

    /// <summary>MoodComponent.GriefLevel >= 0.7. Hand-to-face, shudder; emits Sigh periodically.</summary>
    Crying,

    /// <summary>IsChokingTag without ChokingComponent (early phase) or sickness. Bent-over cough motion; emits Cough per cycle.</summary>
    CoughingFit,

    /// <summary>IntendedAction.Kind == Rescue and victim has IsChokingTag. Behind-victim hugging motion.</summary>
    Heimlich,
}
