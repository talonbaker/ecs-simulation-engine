namespace APIFramework.Systems.Rescue;

/// <summary>Identifies which rescue intervention applies to a given victim.</summary>
public enum RescueKind
{
    /// <summary>Choking victim — rescuer performs the Heimlich maneuver.</summary>
    Heimlich,
    /// <summary>Collapsed victim with no choking or lockout marker — rescuer performs CPR.</summary>
    CPR,
    /// <summary>Locked-in victim — rescuer detaches the obstructing door/obstacle.</summary>
    DoorUnlock,
}
