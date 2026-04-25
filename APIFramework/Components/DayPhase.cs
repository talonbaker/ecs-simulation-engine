namespace APIFramework.Components;

/// <summary>Six-phase day cycle. Integer values mirror Warden.Contracts.Telemetry.DayPhase.</summary>
public enum DayPhase
{
    Night        = 0,
    EarlyMorning = 1,
    MidMorning   = 2,
    Afternoon    = 3,
    Evening      = 4,
    Dusk         = 5,
}
