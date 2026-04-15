namespace APIFramework.Components;

// Average adult male comfortable stomach capacity: ~1000ml
// Fill is normalized 0.0 (completely empty) → 1.0 (completely full)
// Bolus and liquid volumes are expressed in ml to match this scale
// TODO: Per-entity stomach size (species, conditions) can be added later via MaxVolumeMl field
public struct StomachComponent
{
    public const float MaxVolumeMl = 1000f; // Average adult male comfortable capacity

    public float CurrentVolumeMl;   // Physical content currently present (0 → MaxVolumeMl)
    public float DigestionRate;     // ml broken down per second (moves content → queued nutrients)

    public float NutritionQueued;   // Nutrition ready to absorb into MetabolismComponent.Nutrition
    public float HydrationQueued;   // Hydration ready to absorb into MetabolismComponent.Hydration

    // Normalized fill: 0.0 = empty, 1.0 = full
    // Systems and UI should read this rather than raw ml
    public float Fill => CurrentVolumeMl / MaxVolumeMl;

    public bool IsEmpty => CurrentVolumeMl <= 0f;
    public bool IsFull  => Fill >= 1.0f;

    public override string ToString() =>
        $"Stomach: {Fill:P0} full ({CurrentVolumeMl:F0}/{MaxVolumeMl}ml) | Queued — Nutr: {NutritionQueued:F1} Hydr: {HydrationQueued:F1}";
}
