namespace APIFramework.Components;

/// <summary>
/// Attached to an NPC when they are actively choking. Mirrors state from LifeStateComponent
/// for convenience (other systems can check Has{ChokingComponent} without inspecting
/// LifeStateComponent fields). Removed when the NPC transitions to Deceased.
/// </summary>
public struct ChokingComponent
{
    /// <summary>SimulationClock.CurrentTick when the choke began.</summary>
    public long ChokeStartTick;

    /// <summary>
    /// Counts down each tick while State == Incapacitated. Mirror of LifeStateComponent.IncapacitatedTickBudget
    /// for clarity. The canonical countdown is owned by LifeStateComponent; this field
    /// is for read convenience.
    /// </summary>
    public int RemainingTicks;

    /// <summary>The size of the bolus that triggered the choke. Used for telemetry and tuning.</summary>
    public float BolusSize;

    /// <summary>Always Choked at v0.1; reserved for future expansion when other choke-like causes land.</summary>
    public CauseOfDeath PendingCause;

    public override string ToString() =>
        $"Choking (started: {ChokeStartTick}, remaining: {RemainingTicks}s, bolus: {BolusSize:F3}ml)";
}
