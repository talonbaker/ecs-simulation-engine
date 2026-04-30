namespace APIFramework.Components;

/// <summary>
/// The large intestine (colon proximal) — water reabsorption and waste concentration.
///
/// PIPELINE POSITION
/// ─────────────────
///   SmallIntestine → LargeIntestine → ColonComponent
///
/// WHAT HAPPENS HERE
/// ─────────────────
/// LargeIntestineSystem processes content each tick:
///   1. Reabsorbs water from the residue → adds Hydration to MetabolismComponent
///      (models electrolyte/water recovery that happens in the ascending colon).
///   2. Concentrates the remaining solid waste by reducing volume over time.
///   3. Transfers the concentrated waste into ColonComponent at MobilityRate.
///
/// The water reabsorption here is a SECONDARY source of hydration — slower and
/// smaller than direct drinking, but meaningful for long-term fluid balance.
///
/// CAPACITY
/// ─────────
/// ~300 ml working capacity. Content here represents a few meals worth of fiber
/// and unabsorbed matter. A healthy entity will see this at 10–50% between meals.
/// </summary>
public struct LargeIntestineComponent
{
    /// <summary>Working capacity in millilitres; <see cref="Fill"/> is normalised against this.</summary>
    public const float MaxVolumeMl = 300f;

    /// <summary>Volume of waste currently in the large intestine (ml).</summary>
    public float ContentVolumeMl;

    /// <summary>
    /// ml of water reabsorbed per game-second.
    /// Absorbed water is added to MetabolismComponent.Hydration.
    /// Default ~0.001 ml/sec — slow but persistent hydration recovery.
    /// </summary>
    public float WaterReabsorptionRate;

    /// <summary>
    /// ml of content processed (moved toward the colon) per game-second.
    /// Concentrating/drying process reduces volume as it travels.
    /// Default ~0.003 ml/sec.
    /// </summary>
    public float MobilityRate;

    /// <summary>
    /// Fraction of processed volume that becomes stool in ColonComponent.
    /// The remainder is water/gas that dissipates.
    /// Default 0.6 (60% of LI content forms stool).
    /// </summary>
    public float StoolFraction;

    // ── Derived ───────────────────────────────────────────────────────────────

    /// <summary>Normalised fill 0.0 – 1.0 against <see cref="MaxVolumeMl"/>.</summary>
    public readonly float Fill    => ContentVolumeMl / MaxVolumeMl;
    /// <summary>True when no content remains in the large intestine.</summary>
    public readonly bool  IsEmpty => ContentVolumeMl <= 0f;

    /// <summary>Debug-friendly fill-percentage and volume summary.</summary>
    public override string ToString() =>
        $"LargeIntestine: {Fill:P0} ({ContentVolumeMl:F1}/{MaxVolumeMl}ml)";
}
