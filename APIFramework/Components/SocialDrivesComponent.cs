namespace APIFramework.Components;

/// <summary>
/// A single social drive: current (live) value and baseline (resting) value.
/// Both fields stay in 0–100. Current drifts; Baseline is stable for the save.
/// </summary>
public struct DriveValue
{
    /// <summary>Live drive value, 0–100. Drifts each tick under DriveDynamicsSystem.</summary>
    public int Current;
    /// <summary>Resting drive value, 0–100. Set at spawn and stable for the save.</summary>
    public int Baseline;
}

/// <summary>
/// Eight social drives per NPC in canonical cast-bible order.
/// DriveDynamicsSystem writes Current each tick; Baseline is set at spawn.
/// Disambiguated from the physiological DriveComponent by the "Social" prefix.
/// </summary>
public struct SocialDrivesComponent
{
    /// <summary>Need to feel part of a group.</summary>
    public DriveValue Belonging;
    /// <summary>Need for relative standing among peers.</summary>
    public DriveValue Status;
    /// <summary>Felt warmth toward others.</summary>
    public DriveValue Affection;
    /// <summary>Felt annoyance with others.</summary>
    public DriveValue Irritation;
    /// <summary>Felt sexual / romantic attraction.</summary>
    public DriveValue Attraction;
    /// <summary>Felt trust in others.</summary>
    public DriveValue Trust;
    /// <summary>Felt suspicion of others.</summary>
    public DriveValue Suspicion;
    /// <summary>Felt isolation / lack of meaningful connection.</summary>
    public DriveValue Loneliness;

    /// <summary>Clamps <paramref name="v"/> to the 0–100 drive range.</summary>
    public static int Clamp0100(int v) => v < 0 ? 0 : v > 100 ? 100 : v;
}
