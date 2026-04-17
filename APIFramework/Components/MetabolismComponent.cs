namespace APIFramework.Components;

/// <summary>
/// The body's top-level physiological state — the one component systems query
/// to know "how is this entity doing right now?"
///
/// Satiation and Hydration are gameplay-facing 0–100 metrics. They rise via digestion
/// and fall via MetabolismSystem's drain rates. BrainSystem reads Hunger/Thirst
/// (derived from these) to score drives.
///
/// NutrientStores is the real biology layer — accumulated macros, vitamins, and
/// minerals extracted from food. Unused in v0.7.0 beyond tracking; in v0.8+ this
/// becomes the input for deficiency/toxicity tags that feed MoodSystem.
/// </summary>
public struct MetabolismComponent
{
    // ── Physiological Resources (0 = depleted, 100 = fully stocked) ─────────
    // These are the gameplay-facing fullness metrics. Systems drain them;
    // digestion refills them.
    public float Satiation;          // Nutritional fullness
    public float Hydration;          // Water level
    public float BodyTemp;           // Body temperature in Celsius
    public float Energy;             // Affects movement and logic speed (future use)

    // ── Nutrient Stores (v0.7.0+) ────────────────────────────────────────────
    /// <summary>
    /// Cumulative nutrients absorbed by the body, minus what metabolism burns.
    /// Macros measured in grams, water in ml, vitamins/minerals in mg.
    /// DigestionSystem adds to this each tick as stomach contents are absorbed.
    /// Future BodyMetabolismSystem will subtract over time (daily burn).
    /// </summary>
    public NutrientProfile NutrientStores;

    // ── Drain Rates (per second at TimeScale 1.0) ────────────────────────────
    public float SatiationDrainRate;          // How fast Satiation depletes (Billy gets hungry)
    public float HydrationDrainRate;          // How fast Hydration depletes (Billy gets thirsty)

    // ── Sleep modifier ────────────────────────────────────────────────────────
    // Metabolism slows significantly during sleep — breathing/sweat are minimal at rest.
    // MetabolismSystem multiplies both drain rates by this value when SleepingTag is present.
    // 0.10 = 10% of awake rate, so an 8-hour sleep only costs ~10% hydration/satiation.
    public float SleepMetabolismMultiplier;   // 0.0 (no drain) to 1.0 (full awake drain)

    // ── Derived Sensations (computed — never set directly) ───────────────────
    // These are what Billy perceives, not the actual physiological state.
    // Future: lag effects, stress modifiers, medications, and mood can adjust these.
    public readonly float Hunger => MathF.Max(0f, 100f - Satiation);
    public readonly float Thirst => MathF.Max(0f, 100f - Hydration);

    public override string ToString() =>
        $"Satiation: {Satiation:F1}  Hydration: {Hydration:F1}  Temp: {BodyTemp:F1}°C  Stores: {NutrientStores}";
}
