namespace APIFramework.Components;

public struct MetabolismComponent
{
    // ── Physiological Resources (0 = depleted, 100 = fully stocked) ─────────
    // These are the actual biological state. Systems drain them; digestion refills them.
    public float Satiation;          // Nutritional fullness — drains over time, refilled by eating
    public float Hydration;          // Water level — drains over time, refilled by drinking
    public float BodyTemp;           // Body temperature in Celsius
    public float Energy;             // Affects movement and logic speed (future use)

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
        $"Satiation: {Satiation:F1}  Hydration: {Hydration:F1}  Temp: {BodyTemp:F1}°C";
}
