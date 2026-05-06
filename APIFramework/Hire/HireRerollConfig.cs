namespace APIFramework.Hire;

/// <summary>
/// Tunable knobs for the hire reroll economy (WP-4.0.N). Defaults match the
/// initial design conversation; future content packets calibrate against playtest.
/// </summary>
public sealed class HireRerollConfig
{
    /// <summary>Tokens charged per <see cref="HireSession.TryReroll"/> call. Default 1.</summary>
    public int  RerollCost  { get; init; } = 1;

    /// <summary>Maximum rerolls per hire session before the cap kicks in. Default 5.</summary>
    public int  MaxRerolls  { get; init; } = 5;

    /// <summary>When true, the cap is enforced; when false, only wallet balance gates rerolls. Default true.</summary>
    public bool CapEnabled  { get; init; } = true;
}
