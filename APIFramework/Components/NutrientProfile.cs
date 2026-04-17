namespace APIFramework.Components;

/// <summary>
/// Structured representation of the nutritional content of a food item, a drink,
/// a bolus in transit, a stomach's queued contents, or a body's accumulated stores.
///
/// DESIGN INTENT
/// ─────────────
/// This struct is the ONE type used everywhere nutrients are represented:
///   - FoodItemConfig.Nutrients  — what a piece of food provides
///   - BolusComponent.Nutrients  — nutrients travelling down the esophagus
///   - LiquidComponent.Nutrients — same, for drinks
///   - StomachComponent.NutrientsQueued — what is queued inside the stomach
///   - MetabolismComponent.NutrientStores — cumulative body-wide stores
///
/// Using a single structured type means the digestive pipeline can simply
/// add / scale / subtract profiles as food moves through the body, with no
/// translation layer between stages.
///
/// UNITS
/// ─────
///   Macros / Fiber  → grams
///   Water           → millilitres (ml)
///   Vitamins        → milligrams (mg)
///   Minerals        → milligrams (mg)
///
/// All vitamin and mineral fields default to 0. They are scaffolded now so the
/// component schema is stable — real values will be populated in v0.8+ when
/// deficiency/toxicity tags become inputs to MoodSystem and BiologicalConditionSystem.
///
/// FORWARD COMPATIBILITY
/// ──────────────────────
/// The same struct flows through every digestive stage (stomach → small intestine →
/// large intestine → absorption). When we add intestine components in v0.7.1+, they
/// will also hold a NutrientProfile representing their current chyme/waste contents.
/// No schema migration will be needed.
/// </summary>
public struct NutrientProfile
{
    // ── Macronutrients (grams) ────────────────────────────────────────────────
    public float Carbohydrates;
    public float Proteins;
    public float Fats;
    public float Fiber;

    // ── Hydration (ml) ────────────────────────────────────────────────────────
    public float Water;

    // ── Vitamins (mg) ─────────────────────────────────────────────────────────
    public float VitaminA;
    public float VitaminB;
    public float VitaminC;
    public float VitaminD;
    public float VitaminE;
    public float VitaminK;

    // ── Minerals (mg) ─────────────────────────────────────────────────────────
    public float Sodium;
    public float Potassium;
    public float Calcium;
    public float Iron;
    public float Magnesium;

    // ── Derived ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Energy content in kilocalories using the Atwater factors:
    ///   Carbs × 4 + Proteins × 4 + Fats × 9
    /// Fiber is counted in carbohydrates by convention and is not added separately.
    /// </summary>
    public readonly float Calories => Carbohydrates * 4f + Proteins * 4f + Fats * 9f;

    /// <summary>True if every field is zero (or within float epsilon of zero).</summary>
    public readonly bool IsEmpty =>
        Carbohydrates < 0.001f && Proteins < 0.001f && Fats < 0.001f && Fiber < 0.001f &&
        Water < 0.001f &&
        VitaminA < 0.001f && VitaminB < 0.001f && VitaminC < 0.001f &&
        VitaminD < 0.001f && VitaminE < 0.001f && VitaminK < 0.001f &&
        Sodium < 0.001f && Potassium < 0.001f && Calcium < 0.001f &&
        Iron < 0.001f && Magnesium < 0.001f;

    // ── Arithmetic — so the digestive pipeline can do `a + b`, `profile * 0.5f` ─

    /// <summary>Combine two profiles (accumulate body stores, merge queued nutrients).</summary>
    public static NutrientProfile operator +(NutrientProfile a, NutrientProfile b) => new()
    {
        Carbohydrates = a.Carbohydrates + b.Carbohydrates,
        Proteins      = a.Proteins      + b.Proteins,
        Fats          = a.Fats          + b.Fats,
        Fiber         = a.Fiber         + b.Fiber,
        Water         = a.Water         + b.Water,
        VitaminA      = a.VitaminA      + b.VitaminA,
        VitaminB      = a.VitaminB      + b.VitaminB,
        VitaminC      = a.VitaminC      + b.VitaminC,
        VitaminD      = a.VitaminD      + b.VitaminD,
        VitaminE      = a.VitaminE      + b.VitaminE,
        VitaminK      = a.VitaminK      + b.VitaminK,
        Sodium        = a.Sodium        + b.Sodium,
        Potassium     = a.Potassium     + b.Potassium,
        Calcium       = a.Calcium       + b.Calcium,
        Iron          = a.Iron          + b.Iron,
        Magnesium     = a.Magnesium     + b.Magnesium,
    };

    /// <summary>Subtract profile b from profile a (remove absorbed nutrients from a queue).</summary>
    public static NutrientProfile operator -(NutrientProfile a, NutrientProfile b) => new()
    {
        Carbohydrates = a.Carbohydrates - b.Carbohydrates,
        Proteins      = a.Proteins      - b.Proteins,
        Fats          = a.Fats          - b.Fats,
        Fiber         = a.Fiber         - b.Fiber,
        Water         = a.Water         - b.Water,
        VitaminA      = a.VitaminA      - b.VitaminA,
        VitaminB      = a.VitaminB      - b.VitaminB,
        VitaminC      = a.VitaminC      - b.VitaminC,
        VitaminD      = a.VitaminD      - b.VitaminD,
        VitaminE      = a.VitaminE      - b.VitaminE,
        VitaminK      = a.VitaminK      - b.VitaminK,
        Sodium        = a.Sodium        - b.Sodium,
        Potassium     = a.Potassium     - b.Potassium,
        Calcium       = a.Calcium       - b.Calcium,
        Iron          = a.Iron          - b.Iron,
        Magnesium     = a.Magnesium     - b.Magnesium,
    };

    /// <summary>Scale every field by a factor (e.g. "release 5% of queued nutrients this tick").</summary>
    public static NutrientProfile operator *(NutrientProfile p, float factor) => new()
    {
        Carbohydrates = p.Carbohydrates * factor,
        Proteins      = p.Proteins      * factor,
        Fats          = p.Fats          * factor,
        Fiber         = p.Fiber         * factor,
        Water         = p.Water         * factor,
        VitaminA      = p.VitaminA      * factor,
        VitaminB      = p.VitaminB      * factor,
        VitaminC      = p.VitaminC      * factor,
        VitaminD      = p.VitaminD      * factor,
        VitaminE      = p.VitaminE      * factor,
        VitaminK      = p.VitaminK      * factor,
        Sodium        = p.Sodium        * factor,
        Potassium     = p.Potassium     * factor,
        Calcium       = p.Calcium       * factor,
        Iron          = p.Iron          * factor,
        Magnesium     = p.Magnesium     * factor,
    };

    /// <summary>Commutative form of scalar multiplication.</summary>
    public static NutrientProfile operator *(float factor, NutrientProfile p) => p * factor;

    public override string ToString() =>
        $"{Calories:F0} kcal  C:{Carbohydrates:F1}g P:{Proteins:F1}g F:{Fats:F1}g  W:{Water:F0}ml";
}
