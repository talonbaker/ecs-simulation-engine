using APIFramework.Cast;

namespace APIFramework.Hire;

/// <summary>
/// Player-perk extension point that biases the tier thresholds used during a hire
/// session. Modders + future content packets author concrete perks by subclassing
/// <see cref="HirePerk"/> and implementing <see cref="Apply"/>. Multiple perks
/// compose: <c>HireRerollService</c> aggregates them in order.
/// </summary>
public abstract class HirePerk
{
    /// <summary>
    /// Apply this perk's bias to the supplied baseline thresholds; return the
    /// modified thresholds. Implementations must NOT mutate <paramref name="baseThresholds"/>;
    /// always return a NEW <see cref="TierThresholdsDto"/>.
    /// </summary>
    public abstract TierThresholdsDto Apply(TierThresholdsDto baseThresholds);
}

/// <summary>
/// Example perk: shifts every cumulative threshold down by <c>shift</c>, which
/// shrinks the Common bucket and expands the Rare/Epic/Legendary buckets. Mythic
/// stays at 1.0 (already capped).
/// </summary>
public sealed class LuckyHirePerk : HirePerk
{
    private readonly double _shift;

    /// <param name="shift">Amount to subtract from each threshold; clamped to ≥ 0 effectively. Typical value 0.05–0.20 (5%–20%).</param>
    public LuckyHirePerk(double shift) { _shift = shift; }

    /// <inheritdoc/>
    public override TierThresholdsDto Apply(TierThresholdsDto t) => new()
    {
        Common    = System.Math.Max(0.0, t.Common    - _shift),
        Uncommon  = System.Math.Max(0.0, t.Uncommon  - _shift),
        Rare      = System.Math.Max(0.0, t.Rare      - _shift),
        Epic      = System.Math.Max(0.0, t.Epic      - _shift),
        Legendary = System.Math.Max(0.0, t.Legendary - _shift),
        Mythic    = 1.0,    // mythic ceiling stays at 1.0 regardless
    };
}
