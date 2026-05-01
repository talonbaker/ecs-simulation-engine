namespace APIFramework.Components;

/// <summary>
/// The colon/rectum — terminal holding vessel for formed stool.
///
/// PIPELINE POSITION
/// ─────────────────
///   LargeIntestine → ColonComponent → [DefecationSystem eliminates waste]
///
/// WHAT HAPPENS HERE
/// ─────────────────
/// ColonSystem accumulates stool from LargeIntestine each tick.
/// When StoolVolumeMl exceeds UrgeThresholdMl:
///   → DefecationUrgeTag is applied (entity has an urge to defecate)
///   → BrainSystem increases DefecateUrgency accordingly
///   → DefecationSystem (Behavior phase) will eventually empty the colon
///      when Defecate becomes the dominant drive
///
/// When StoolVolumeMl exceeds CapacityMl:
///   → BowelCriticalTag is applied (overrides other drives)
///
/// At TimeScale 120, a well-fed human will reach UrgeThresholdMl roughly once
/// per game-day — matching biological frequency (~1–2 bowel movements per day).
///
/// DESIGN NOTE
/// ───────────
/// The ColonComponent deliberately does NOT contain a NutrientProfile — all
/// nutritional absorption has completed upstream. This component is purely a
/// volume-tracking vessel for the elimination drive.
/// </summary>
public struct ColonComponent
{
    /// <summary>Volume at which DefecationUrgeTag is applied (entity feels the urge).</summary>
    public float UrgeThresholdMl;

    /// <summary>Volume at which BowelCriticalTag is applied (overriding urgency).</summary>
    public float CapacityMl;

    /// <summary>Formed stool currently held in the colon/rectum (ml).</summary>
    public float StoolVolumeMl;

    // ── Derived ───────────────────────────────────────────────────────────────

    /// <summary>Normalised fill 0.0 (empty) – 1.0 (at <see cref="CapacityMl"/>).</summary>
    public readonly float Fill       => CapacityMl > 0 ? StoolVolumeMl / CapacityMl : 0f;
    /// <summary>True when <see cref="StoolVolumeMl"/> ≥ <see cref="UrgeThresholdMl"/>.</summary>
    public readonly bool  HasUrge    => StoolVolumeMl >= UrgeThresholdMl;
    /// <summary>True when <see cref="StoolVolumeMl"/> ≥ <see cref="CapacityMl"/>.</summary>
    public readonly bool  IsCritical => StoolVolumeMl >= CapacityMl;
    /// <summary>True when no stool remains in the colon.</summary>
    public readonly bool  IsEmpty    => StoolVolumeMl <= 0f;

    /// <summary>Debug-friendly volume/threshold/state summary.</summary>
    public override string ToString() =>
        $"Colon: {StoolVolumeMl:F1}ml (urge at {UrgeThresholdMl:F0}ml, cap {CapacityMl:F0}ml)" +
        (IsCritical ? " CRITICAL" : HasUrge ? " URGE" : "");
}
