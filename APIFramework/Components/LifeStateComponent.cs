namespace APIFramework.Components;

/// <summary>
/// Life state of an NPC: Alive, Incapacitated (biologically alive but non-volitional),
/// or Deceased (dead and inert).
/// </summary>
public enum LifeState
{
    /// <summary>NPC is fully alive: cognition, volition, and physiology all tick normally.</summary>
    Alive = 0,
    /// <summary>NPC is biologically alive but non-volitional (e.g. choking, knocked out).
    /// Physiology continues, cognition is suspended; an IncapacitatedTickBudget counts down to Deceased.</summary>
    Incapacitated = 1,
    /// <summary>NPC is dead and inert. Terminal state — no resurrection.</summary>
    Deceased = 2
}

/// <summary>
/// Primary life-state component. Attached to every NPC at spawn.
/// Transitions: Alive → Incapacitated → Deceased (no resurrection).
/// The LifeStateTransitionSystem is the single writer of this component.
/// </summary>
public struct LifeStateComponent
{
    /// <summary>Current state: Alive, Incapacitated, or Deceased.</summary>
    public LifeState State;

    /// <summary>SimulationClock.CurrentTick at the most recent state transition.</summary>
    public long LastTransitionTick;

    /// <summary>
    /// While State == Incapacitated, this counts down each tick (from DefaultIncapacitatedTicks).
    /// When it reaches 0, the NPC transitions to Deceased using PendingDeathCause.
    /// Ignored for other states.
    /// </summary>
    public int IncapacitatedTickBudget;

    /// <summary>
    /// While State == Incapacitated, this carries the CauseOfDeath that will register
    /// if the IncapacitatedTickBudget expires. Used by the timeout-to-Deceased transition.
    /// </summary>
    public CauseOfDeath PendingDeathCause;
}
