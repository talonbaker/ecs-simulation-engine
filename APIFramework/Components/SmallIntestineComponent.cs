namespace APIFramework.Components;

/// <summary>
/// The small intestine — the primary site of nutrient absorption in the digestive tract.
///
/// PIPELINE POSITION
/// ─────────────────
///   Stomach → DigestionSystem → SmallIntestineComponent → SmallIntestineSystem
///                             → LargeIntestineComponent → LargeIntestineSystem
///                             → (v0.7.3) RectumComponent
///
/// DigestionSystem hands off partially-digested chyme (volume + NutrientProfile)
/// into this component each tick, instead of writing directly to NutrientStores
/// as it did in v0.7.0. SmallIntestineSystem then processes contents at its own
/// absorption rate, extracting macronutrients, vitamins, and a fraction of water
/// into MetabolismComponent.NutrientStores. The unabsorbed residue (fiber, the
/// unabsorbed water fraction, and the mineral fraction that escaped) is forwarded
/// to LargeIntestineComponent.
///
/// WHY THIS IS A COMPONENT, NOT AN ENTITY
/// ────────────────────────────────────────
/// Organs are stateful bags attached to a body entity. The small intestine doesn't
/// travel independently — it has a fixed relationship to the entity it belongs to,
/// and its contents only ever flow linearly to the next organ. Only food boluses
/// (which physically transit the esophagus and could in principle be redirected)
/// need to be entities. Everything downstream of the stomach is a component on the
/// body entity, consistent with StomachComponent.
///
/// CAPACITY
/// ─────────
/// An adult small intestine can hold roughly 200–500 ml of chyme at once. We use
/// 200 ml as the soft cap. The stomach's DigestionRate (0.017 ml/game-s) is slow
/// enough that the SI is rarely near capacity — the cap exists to produce correct
/// backpressure behaviour if the simulation is somehow overwhelmed (e.g., extreme
/// time-scale values). FeedingSystem's own queue cap on the stomach is the practical
/// upper limit under normal operating conditions.
/// </summary>
public struct SmallIntestineComponent
{
    /// <summary>Soft capacity limit in ml. DigestionSystem will not push more than this.</summary>
    public const float CapacityMl = 200f;

    /// <summary>Current volume of chyme in transit through the small intestine (ml).</summary>
    public float CurrentVolumeMl;

    /// <summary>
    /// Full nutritional breakdown of chyme currently present in the small intestine.
    /// SmallIntestineSystem drains this each tick, depositing absorbed fractions into
    /// MetabolismComponent.NutrientStores and forwarding residue to LargeIntestineComponent.
    /// </summary>
    public NutrientProfile Contents;

    /// <summary>Normalised fill: 0.0 = empty, 1.0 = at capacity. Used by UI and InvariantSystem.</summary>
    public readonly float Fill => CurrentVolumeMl / CapacityMl;

    public readonly bool IsEmpty => CurrentVolumeMl <= 0f;

    public override string ToString() =>
        $"SmallIntestine: {Fill:P0} ({CurrentVolumeMl:F1}/{CapacityMl:F0} ml) | {Contents.Calories:F0} kcal queued";
}
