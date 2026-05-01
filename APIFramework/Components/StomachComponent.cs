namespace APIFramework.Components;

/// <summary>
/// The stomach — temporary holding vessel where swallowed boluses sit in acid,
/// dissolving into nutrients that DigestionSystem then releases into the body.
///
/// PIPELINE POSITION IN THE DIGESTIVE TRACT
/// ─────────────────────────────────────────
///   Esophagus  →  Stomach  →  (future) SmallIntestine  →  LargeIntestine  →  Rectum
///
/// In v0.7.0 the stomach is still the final absorption stage: DigestionSystem
/// drains NutrientsQueued directly into MetabolismComponent.NutrientStores,
/// with derived updates to Satiation and Hydration. Intestines land in v0.7.1+.
///
/// CAPACITY
/// ─────────
/// Average adult male comfortable stomach capacity: ~1000 ml. IsFull at 1.0 fill.
/// TODO: per-entity stomach size (species, conditions) via a MaxVolumeMl field.
/// </summary>
public struct StomachComponent
{
    /// <summary>Comfortable adult capacity in millilitres; <see cref="Fill"/> is normalised against this.</summary>
    public const float MaxVolumeMl = 1000f;

    /// <summary>Physical content currently present in the stomach (ml).</summary>
    public float CurrentVolumeMl;

    /// <summary>Volume broken down per game-second (converts content → released nutrients).</summary>
    public float DigestionRate;

    /// <summary>
    /// Full nutritional breakdown of everything currently sitting in the stomach,
    /// not yet absorbed by the body. Each tick DigestionSystem releases a fraction
    /// proportional to the volume digested.
    /// </summary>
    public NutrientProfile NutrientsQueued;

    /// <summary>Normalised fill: 0.0 = empty, 1.0 = full. Systems/UI should read this.</summary>
    public readonly float Fill => CurrentVolumeMl / MaxVolumeMl;

    /// <summary>True when no content remains in the stomach.</summary>
    public readonly bool IsEmpty => CurrentVolumeMl <= 0f;
    /// <summary>True when the stomach is at or above <see cref="MaxVolumeMl"/>.</summary>
    public readonly bool IsFull  => Fill >= 1.0f;

    /// <summary>Debug-friendly fill-percentage and queued-nutrient summary.</summary>
    public override string ToString() =>
        $"Stomach: {Fill:P0} full ({CurrentVolumeMl:F0}/{MaxVolumeMl}ml) | Queued: {NutrientsQueued}";
}
