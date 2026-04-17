using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Drains the stomach over time and hands chyme to the small intestine.
///
/// PIPELINE POSITION
/// ─────────────────
///   FeedingSystem → EsophagusSystem → StomachComponent → DigestionSystem
///                → SmallIntestineComponent → SmallIntestineSystem → ...
///
/// v0.7.0 CHANGE — BIOLOGY LAYER
/// ──────────────────────────────
/// Food is a NutrientProfile (macros, water, vitamins, minerals) rather than flat
/// scalar points. Each tick a ratio of stomach volume is digested, the same fraction
/// of NutrientsQueued is released, and Satiation/Hydration are updated via configurable
/// conversion factors (SatiationPerCalorie, HydrationPerMl).
///
/// v0.7.1 CHANGE — INTESTINE HANDOFF
/// ───────────────────────────────────
/// In v0.7.0 DigestionSystem wrote released nutrients directly into
/// MetabolismComponent.NutrientStores. That was an acknowledged shortcut — the
/// FORWARD COMPAT note in the original DigestionSystem comment anticipated exactly
/// this change. Now chyme goes to SmallIntestineComponent.Contents instead, and
/// SmallIntestineSystem handles the per-nutrient absorption fractions and the
/// NutrientStores write.
///
/// The Satiation/Hydration conversion REMAINS HERE by design. Physiologically,
/// the feeling of fullness begins in the stomach — stretch receptors signal satiation
/// as the stomach fills and empties, and hormones like CCK are released during gastric
/// digestion, not intestinal absorption. Keeping this conversion in DigestionSystem
/// correctly represents "Billy feels full because his stomach is digesting", while
/// SmallIntestineSystem fills the real-biology NutrientStores pool for v0.8+.
///
/// BACKPRESSURE
/// ─────────────
/// If the small intestine is at capacity, DigestionSystem stops releasing chyme
/// until space opens up. This creates realistic gastric backpressure: a plugged
/// intestine means the stomach cannot empty, which means eating triggers IsFull
/// sooner. The FeedingSystem's NutritionQueueCap prevents the stomach from
/// overfilling in the first place, so backpressure is only a concern at
/// extreme time-scale values or very high feeding rates.
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

            // v0.7.1+: chyme must have somewhere to go. If the entity has no
            // SmallIntestineComponent, skip — this system no longer writes directly
            // to NutrientStores. All digestive entities (human, cat) receive a
            // SmallIntestineComponent via EntityTemplates.
            if (!entity.Has<SmallIntestineComponent>()) continue;
            if (!entity.Has<MetabolismComponent>())     continue;

            var si   = entity.Get<SmallIntestineComponent>();
            var meta = entity.Get<MetabolismComponent>();

            // How much capacity the small intestine can still receive.
            float receivableVolume = SmallIntestineComponent.CapacityMl - si.CurrentVolumeMl;
            if (receivableVolume <= 0f) continue; // SI full — stomach backs up this tick

            // How much volume the stomach wants to release this tick.
            float wantToDigest = MathF.Min(stomach.DigestionRate * deltaTime, stomach.CurrentVolumeMl);

            // Actual release is the lesser of what the stomach wants to release
            // and what the SI can receive. Under normal parameters these are identical.
            float digested = MathF.Min(wantToDigest, receivableVolume);

            // Ratio of the stomach's content digested this tick — nutrients release
            // at the same proportional rate as volume drains.
            float ratio = digested / stomach.CurrentVolumeMl;

            // Extract the proportional nutrient slice from the stomach queue.
            NutrientProfile released = stomach.NutrientsQueued * ratio;

            stomach.CurrentVolumeMl -= digested;
            stomach.NutrientsQueued -= released;

            // Numerical hygiene — prevent tiny negatives from floating-point drift.
            if (stomach.CurrentVolumeMl < 0f) stomach.CurrentVolumeMl = 0f;

            entity.Add(stomach);

            // ── Hand chyme to the small intestine ─────────────────────────────
            // This is the v0.7.1 change: instead of depositing released nutrients
            // straight into NutrientStores, we queue them into the SI for per-nutrient
            // absorption via SmallIntestineSystem. The volume tracking mirrors the
            // StomachComponent pattern exactly.
            si.CurrentVolumeMl += digested;
            si.Contents        += released;

            entity.Add(si);

            // ── Derive gameplay metrics from the release ───────────────────────
            // Satiation and Hydration are updated HERE (not in SmallIntestineSystem)
            // because they represent stomach-level fullness, not intestinal absorption.
            // See the class-level comment for the physiology reasoning.
            //
            // Tuning invariant (must not change without re-calibrating the whole chain):
            //   banana ~117 kcal × 0.3 SatiationPerCalorie ≈ 35 satiation gain
            //   water  15 ml    × 2.0 HydrationPerMl        = 30 hydration gain
            float satiationGain = released.Calories * _cfg.SatiationPerCalorie;
            float hydrationGain = released.Water    * _cfg.HydrationPerMl;

            meta.Satiation = MathF.Min(100f, meta.Satiation + satiationGain);
            meta.Hydration = MathF.Min(100f, meta.Hydration + hydrationGain);

            entity.Add(meta);
        }
    }
}
