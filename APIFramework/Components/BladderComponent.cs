namespace APIFramework.Components;

/// <summary>
/// The urinary bladder — holds urine until the entity voids.
///
/// PIPELINE POSITION
/// ─────────────────
///   BladderFillSystem (Physiology/10) fills the bladder each tick.
///   BladderSystem     (Elimination/55) applies/removes urge tags.
///   UrinationSystem   (Behavior/40)   empties the bladder when Pee is dominant.
///
/// FILL MODEL
/// ──────────
/// The bladder fills at a constant FillRate (ml per game-second). This is a
/// deliberate simplification — kidneys are omitted since their only visible
/// effect would be filtering toxins, which requires an intoxication system
/// that doesn't exist yet. The flat fill rate produces realistic pee frequency
/// without invisible kidney plumbing.
///
/// CAPACITY
/// ─────────
/// Functional human bladder ≈ 400–600 ml; this component uses a game-scale
/// capacity (default 100 ml) so urgency is reached within a normal game-day.
/// UrgeThresholdMl = 70% fill → comfortable first awareness.
/// CapacityMl      = 100%     → critical, entity must pee now.
///
/// At TimeScale 120 with FillRate 0.010 ml/game-second, the human reaches
/// UrgencyThreshold in ~2 game-hours — roughly 6–8 bathroom trips per game-day.
/// </summary>
public struct BladderComponent
{
    /// <summary>Current urine volume in the bladder (ml).</summary>
    public float VolumeML;

    /// <summary>
    /// ml of urine produced per game-second.
    /// At TimeScale 120, 0.010 ml/sec → urge threshold in ~2 game-hours.
    /// </summary>
    public float FillRate;

    /// <summary>Volume at which UrinationUrgeTag is applied.</summary>
    public float UrgeThresholdMl;

    /// <summary>Volume at which BladderCriticalTag is applied (entity must pee immediately).</summary>
    public float CapacityMl;

    // ── Derived ───────────────────────────────────────────────────────────────

    /// <summary>Normalised fill 0.0 (empty) – 1.0 (at <see cref="CapacityMl"/>).</summary>
    public readonly float Fill       => CapacityMl > 0f ? VolumeML / CapacityMl : 0f;
    /// <summary>True when <see cref="VolumeML"/> ≥ <see cref="UrgeThresholdMl"/>.</summary>
    public readonly bool  HasUrge    => VolumeML >= UrgeThresholdMl;
    /// <summary>True when <see cref="VolumeML"/> ≥ <see cref="CapacityMl"/>.</summary>
    public readonly bool  IsCritical => VolumeML >= CapacityMl;
    /// <summary>True when no urine remains in the bladder.</summary>
    public readonly bool  IsEmpty    => VolumeML <= 0f;

    /// <summary>Debug-friendly volume/threshold/state summary.</summary>
    public override string ToString() =>
        $"Bladder: {VolumeML:F1}ml (urge at {UrgeThresholdMl:F0}ml, cap {CapacityMl:F0}ml)" +
        (IsCritical ? " CRITICAL" : HasUrge ? " URGE" : "");
}
