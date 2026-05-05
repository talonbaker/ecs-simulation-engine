namespace APIFramework.Components;

/// <summary>
/// The small intestine — primary site of detailed nutrient absorption.
///
/// PIPELINE POSITION
/// -----------------
///   Stomach → SmallIntestine → LargeIntestine → ColonComponent
///
/// WHAT HAPPENS HERE
/// -----------------
/// When the stomach digests content it transfers a residue fraction into this
/// component (done by DigestionSystem when ResidueFraction > 0 and this component
/// is present on the entity). That residue is "chyme" — a semi-liquid mixture of
/// partially-digested food, water, and micronutrients.
///
/// SmallIntestineSystem processes the chyme over time:
///   - Absorbs micronutrients (vitamins, minerals) into MetabolismComponent.NutrientStores
///   - Drains ChymeVolumeMl at AbsorptionRate per game-second
///   - When content empties, transfers the indigestible residue (fiber + unabsorbed
///     water) into LargeIntestineComponent
///
/// CAPACITY
/// ---------
/// Functional chyme capacity ~250 ml. This is the processed volume per meal cycle;
/// a well-fed entity will regularly see this at 30–80% fill between meals.
/// </summary>
public struct SmallIntestineComponent
{
    /// <summary>Working capacity in millilitres; <see cref="Fill"/> is normalised against this.</summary>
    public const float MaxVolumeMl = 250f;

    /// <summary>Volume of chyme currently in transit through the small intestine (ml).</summary>
    public float ChymeVolumeMl;

    /// <summary>
    /// ml of chyme processed per game-second.
    /// Determines how long nutrients stay in the small intestine.
    /// Default: ~0.008 ml/sec → a 25 ml meal residue takes ~52 game-minutes.
    /// </summary>
    public float AbsorptionRate;

    /// <summary>
    /// Nutrients still in the chyme awaiting absorption.
    /// SmallIntestineSystem will extract vitamins/minerals into NutrientStores.
    /// </summary>
    public NutrientProfile Chyme;

    /// <summary>
    /// Fraction of processed volume transferred to LargeIntestineComponent as residue.
    /// The remainder (1 - ResidueToLargeFraction) is "absorbed" and effectively zero.
    /// Default 0.4 means 40% of SI content becomes LI waste.
    /// </summary>
    public float ResidueToLargeFraction;

    // -- Derived ---------------------------------------------------------------

    /// <summary>Normalised fill 0.0 – 1.0 against <see cref="MaxVolumeMl"/>.</summary>
    public readonly float Fill    => ChymeVolumeMl / MaxVolumeMl;
    /// <summary>True when no chyme remains in the small intestine.</summary>
    public readonly bool  IsEmpty => ChymeVolumeMl <= 0f;

    /// <summary>Debug-friendly fill-percentage and volume summary.</summary>
    public override string ToString() =>
        $"SmallIntestine: {Fill:P0} ({ChymeVolumeMl:F1}/{MaxVolumeMl}ml)";
}
