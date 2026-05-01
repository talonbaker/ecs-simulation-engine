namespace APIFramework.Components;

/// <summary>
/// A ball of chewed food traveling down the esophagus toward the stomach.
///
/// The `Nutrients` field is the full biological breakdown of what this bolus will
/// release into the stomach once it arrives — macros, water, fiber, vitamins, minerals.
/// DigestionSystem absorbs these proportionally as the stomach churns.
///
/// Volume is physical stomach displacement (ml), independent of the nutrient water
/// content — a dry cracker has low Water but non-trivial Volume.
/// </summary>
public struct BolusComponent
{
    /// <summary>Physical stomach displacement (ml) once swallowed.</summary>
    public float Volume;

    /// <summary>Full nutritional breakdown released to the stomach on arrival.</summary>
    public NutrientProfile Nutrients;

    /// <summary>Chew resistance 0.0 (soft, e.g. banana) → 1.0 (very tough, e.g. jerky).</summary>
    public float Toughness;

    /// <summary>Human-readable food label ("Banana", "Steak", etc.).</summary>
    public string FoodType;

    /// <summary>Debug-friendly food/calorie/volume summary.</summary>
    public override string ToString() =>
        $"{FoodType} ({Nutrients.Calories:F0} kcal | Vol: {Volume:F0}ml)";
}
