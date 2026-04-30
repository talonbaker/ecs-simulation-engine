namespace APIFramework.Components;

/// <summary>Six-phase day cycle. Integer values mirror Warden.Contracts.Telemetry.DayPhase.</summary>
public enum DayPhase
{
    /// <summary>Deep night.</summary>
    Night        = 0,
    /// <summary>Sunrise / early morning hours.</summary>
    EarlyMorning = 1,
    /// <summary>Late morning before noon.</summary>
    MidMorning   = 2,
    /// <summary>Post-noon afternoon.</summary>
    Afternoon    = 3,
    /// <summary>Late afternoon / early evening.</summary>
    Evening      = 4,
    /// <summary>Sunset transition into night.</summary>
    Dusk         = 5,
}
