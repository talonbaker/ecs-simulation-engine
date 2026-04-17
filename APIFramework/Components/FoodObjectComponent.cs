namespace APIFramework.Components;

/// <summary>
/// A physical food object the entity is currently holding (e.g. a sandwich from the fridge).
/// InteractionSystem takes bites from it, emitting bolus entities into the esophagus.
///
/// Each bite carries NutrientsPerBite — the fraction of the whole food's nutritional
/// content released per chew. Summing NutrientsPerBite × BitesRemaining should
/// approximate the food's total NutrientProfile.
///
/// FORWARD COMPAT
/// ──────────────
/// When the world has a fridge/counter entity, InteractionSystem will fetch a
/// FoodObjectComponent from that source and add it to the entity's hand. The
/// FoodObjectComponent then drains one bite at a time until empty.
/// </summary>
public struct FoodObjectComponent
{
    /// <summary>Human-readable food label shown on bolus entities ("Banana", "Apple").</summary>
    public string Name;

    /// <summary>Nutrient content released per bite.</summary>
    public NutrientProfile NutrientsPerBite;

    /// <summary>Remaining bites before the food is consumed and removed from hand.</summary>
    public int BitesRemaining;

    /// <summary>Chew resistance 0.0 (soft) → 1.0 (very tough) — passed through to the bolus.</summary>
    public float Toughness;
}
