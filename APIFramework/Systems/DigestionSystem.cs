using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Drains the stomach over time and releases queued nutrients into the body.
///
/// PIPELINE POSITION
/// ─────────────────
///   FeedingSystem → EsophagusSystem → StomachComponent → DigestionSystem → MetabolismComponent
///
/// v0.7.0 BIOLOGY LAYER
/// ────────────────────
/// Food is now a <see cref="NutrientProfile"/> (macros, water, vitamins, minerals) rather
/// than flat scalar points. Each tick we compute the ratio of stomach volume digested,
/// release that same fraction of the queued NutrientProfile, and:
///
///   1. Accumulate the released nutrients into MetabolismComponent.NutrientStores
///      (the ongoing biology state that future organ-systems will draw from).
///   2. Convert them into the gameplay-facing 0-100 metrics via configurable factors:
///        - Released Calories * SatiationPerCalorie → Satiation
///        - Released Water    * HydrationPerMl      → Hydration
///
/// The factors are tuned so behaviour matches pre-v0.7 tuning out of the box
/// (banana ≈117 kcal * 0.3 ≈ 35 satiation; 15 ml water * 2.0 = 30 hydration).
///
/// FORWARD COMPAT (v0.7.1+)
/// ────────────────────────
/// When the SmallIntestine component lands, DigestionSystem will hand off partially-
/// digested chyme to it instead of depositing directly into NutrientStores. The
/// Satiation/Hydration conversion stays here; downstream organs drive long-term
/// deficiency/toxicity tags (MoodSystem in v0.8+).
/// </summary>
public class DigestionSystem : ISystem
{
    private readonly DigestionSystemConfig _cfg;

    public DigestionSystem(DigestionSystemConfig cfg)
    {
        _cfg = cfg;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<StomachComponent>().ToList())
        {
            var stomach = entity.Get<StomachComponent>();
            if (stomach.IsEmpty) continue;

            // How much volume is broken down this tick (clamped to what's actually in the stomach).
            float digested = MathF.Min(stomach.DigestionRate * deltaTime, stomach.CurrentVolumeMl);

            // Ratio of the stomach's content digested this tick — nutrients are
            // released at the same fraction as volume drains.
            float ratio = digested / stomach.CurrentVolumeMl;

            // Extract a proportional slice of the queued nutrients to release into the body.
            NutrientProfile released = stomach.NutrientsQueued * ratio;

            stomach.CurrentVolumeMl -= digested;
            stomach.NutrientsQueued -= released;

            // Numerical hygiene — prevent tiny negatives from floating-point drift.
            if (stomach.CurrentVolumeMl < 0f) stomach.CurrentVolumeMl = 0f;

            entity.Add(stomach);

            if (!entity.Has<MetabolismComponent>()) continue;

            var meta = entity.Get<MetabolismComponent>();

            // ── 1. Accumulate into the biology layer ─────────────────────────────
            // Real grams of carbs/protein/fat, ml of water, mg of vitamins/minerals
            // pool into NutrientStores. Future organ-systems will drain from here.
            meta.NutrientStores += released;

            // ── 2. Derive gameplay metrics from the release ──────────────────────
            // Calories  → Satiation (how fed Billy feels)
            // Water (ml) → Hydration (how quenched Billy feels)
            float satiationGain = released.Calories * _cfg.SatiationPerCalorie;
            float hydrationGain = released.Water    * _cfg.HydrationPerMl;

            meta.Satiation = MathF.Min(100f, meta.Satiation + satiationGain);
            meta.Hydration = MathF.Min(100f, meta.Hydration + hydrationGain);

            entity.Add(meta);
        }
    }
}
