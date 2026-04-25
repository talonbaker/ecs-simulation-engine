namespace APIFramework.Components;

/// <summary>
/// A single social drive: current (live) value and baseline (resting) value.
/// Both fields stay in 0–100. Current drifts; Baseline is stable for the save.
/// </summary>
public struct DriveValue
{
    public int Current;
    public int Baseline;
}

/// <summary>
/// Eight social drives per NPC in canonical cast-bible order.
/// DriveDynamicsSystem writes Current each tick; Baseline is set at spawn.
/// Disambiguated from the physiological DriveComponent by the "Social" prefix.
/// </summary>
public struct SocialDrivesComponent
{
    public DriveValue Belonging;
    public DriveValue Status;
    public DriveValue Affection;
    public DriveValue Irritation;
    public DriveValue Attraction;
    public DriveValue Trust;
    public DriveValue Suspicion;
    public DriveValue Loneliness;

    /// <summary>Clamps <paramref name="v"/> to the 0–100 drive range.</summary>
    public static int Clamp0100(int v) => v < 0 ? 0 : v > 100 ? 100 : v;
}
