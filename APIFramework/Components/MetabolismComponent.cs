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
    // -- Physiological Resources (0 = depleted, 100 = fully stocked) ---------
    // These are the gameplay-facing fullness metrics. Systems drain them;
    // digestion refills them.

    /// <summary>Nutritional fullness, 0–100. 0 = starving, 100 = fully fed.</summary>
    public float Satiation;          // Nutritional fullness
    /// <summary>Water level, 0–100. 0 = severely dehydrated, 100 = fully hydrated.</summary>
    public float Hydration;          // Water level
    /// <summary>Body temperature in degrees Celsius.</summary>
    public float BodyTemp;           // Body temperature in Celsius
    /// <summary>Reserved for future use — affects movement/logic speed.</summary>
    public float Energy;             // Affects movement and logic speed (future use)

    // -- Nutrient Stores (v0.7.0+) --------------------------------------------
    /// <summary>
    /// Cumulative nutrients absorbed by the body, minus what metabolism burns.
    /// Macros measured in grams, water in ml, vitamins/minerals in mg.
    /// DigestionSystem adds to this each tick as stomach contents are absorbed.
    /// Future BodyMetabolismSystem will subtract over time (daily burn).
    /// </summary>
    public NutrientProfile NutrientStores;

    // -- Drain Rates (per second at TimeScale 1.0) ----------------------------

    /// <summary>How fast <see cref="Satiation"/> depletes per game-second at TimeScale 1.0.</summary>
    public float SatiationDrainRate;          // How fast Satiation depletes (Billy gets hungry)
    /// <summary>How fast <see cref="Hydration"/> depletes per game-second at TimeScale 1.0.</summary>
    public float HydrationDrainRate;          // How fast Hydration depletes (Billy gets thirsty)

    // -- Sleep modifier --------------------------------------------------------
    // Metabolism slows significantly during sleep — breathing/sweat are minimal at rest.
    // MetabolismSystem multiplies both drain rates by this value when SleepingTag is present.
    // 0.10 = 10% of awake rate, so an 8-hour sleep only costs ~10% hydration/satiation.

    /// <summary>
    /// Multiplier applied to drain rates while <c>SleepingTag</c> is present. 0.0 = no drain,
    /// 1.0 = full awake drain. Default 0.10 → an 8h sleep costs ~10% of awake drain.
    /// </summary>
    public float SleepMetabolismMultiplier;   // 0.0 (no drain) to 1.0 (full awake drain)

    // -- Derived Sensations (computed — never set directly) -------------------
    // These are what Billy perceives, not the actual physiological state.
    // Future: lag effects, stress modifiers, medications, and mood can adjust these.

    /// <summary>Perceived hunger, 0–100. Equals 100 - <see cref="Satiation"/> (clamped at 0).</summary>
    public readonly float Hunger => MathF.Max(0f, 100f - Satiation);
    /// <summary>Perceived thirst, 0–100. Equals 100 - <see cref="Hydration"/> (clamped at 0).</summary>
    public readonly float Thirst => MathF.Max(0f, 100f - Hydration);

    /// <summary>Debug-friendly satiation/hydration/temperature/stores summary.</summary>
    public override string ToString() =>
        $"Satiation: {Satiation:F1}  Hydration: {Hydration:F1}  Temp: {BodyTemp:F1}°C  Stores: {NutrientStores}";
}
