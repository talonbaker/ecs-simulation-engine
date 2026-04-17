using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Absorbs nutrients from chyme in the small intestine into the body's stores,
/// then forwards the unabsorbed residue to the large intestine.
///
/// PIPELINE POSITION
/// ─────────────────
///   DigestionSystem → SmallIntestineComponent → SmallIntestineSystem
///                                             → MetabolismComponent.NutrientStores
///                                             → LargeIntestineComponent
///
/// BIOLOGY
/// ───────
/// The small intestine is where the vast majority of nutrient absorption occurs.
/// Villi and microvilli dramatically increase the surface area available for
/// absorption, and different nutrient classes absorb at different efficiencies:
///
///   Carbohydrates     ~98%   broken into glucose, absorbed rapidly by active transport
///   Proteins          ~92%   broken into amino acids, actively absorbed
///   Fats              ~90%   emulsified by bile into micelles, absorbed by diffusion
///   Fiber              ~0%   indigestible by human enzymes; passes entirely to colon
///   Water             ~50%   half absorbed here by osmosis; large intestine recaptures the rest
///   Fat-soluble vits  ~80%   A, D, E, K absorbed with dietary fat into lymphatics
///   Water-soluble vits ~85%  B-complex and C absorbed in the jejunum
///   Minerals          ~50%   rough average; potassium absorbs well, iron and calcium poorly
///
/// All fractions are configurable in SmallIntestineSystemConfig. A single mineral
/// fraction is used in v0.7.1 for simplicity; v0.8 can give each mineral its own
/// fraction if deficiency modeling demands it (iron absorption is the most clinically
/// significant outlier, at ~15%).
///
/// RATE MODEL
/// ──────────
/// SmallIntestineSystem uses the same ratio-proportional approach as DigestionSystem:
/// each tick it drains a volume of chyme proportional to AbsorptionRate * deltaTime,
/// then applies per-nutrient absorption fractions to that batch.
///
///   Tuned default: AbsorptionRate = 0.004 ml/game-s
///   A single banana's chyme (~50 ml) clears the SI in:
///     50 ml / 0.004 ml/s = 12,500 game-s ≈ 3.5 game-hours at 1× timescale
///   At the default 120× timescale, this is ~1.75 minutes of real time —
///   observable within a single session.
///
/// SATIATION / HYDRATION NOTE
/// ──────────────────────────
/// These gameplay metrics are NOT updated here. DigestionSystem already converts
/// released Calories → Satiation and released Water → Hydration at the point chyme
/// leaves the stomach. Physiologically, the feeling of satiation begins in the
/// stomach itself (via stretch receptors and gut hormones like CCK), not downstream.
/// SmallIntestineSystem's job is to fill NutrientStores — the real-biology pool
/// that v0.8's BodyMetabolismSystem and NutrientDeficiencySystem will consume.
///
/// BACKPRESSURE
/// ─────────────
/// DigestionSystem caps what it passes to the SI at the SI's remaining capacity.
/// SmallIntestineSystem does not need to handle overflow — it processes whatever
/// is in Contents at its own rate, which is slower than the stomach's release rate.
/// The stomach therefore acts as the primary buffer; SI fills gradually.
/// </summary>
public class SmallIntestineSystem : ISystem
{
    private readonly SmallIntestineSystemConfig _cfg;

    public SmallIntestineSystem(SmallIntestineSystemConfig cfg)
    {
        _cfg = cfg;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        // We query by SmallIntestineComponent. In practice this is Billy (and future
        // human entities). Only entities with the full digestive pipeline wired up
        // should have this component (see EntityTemplates).
        foreach (var entity in em.Query<SmallIntestineComponent>().ToList())
        {
            if (!entity.Has<LargeIntestineComponent>()) continue;
            if (!entity.Has<MetabolismComponent>())     continue;

            var si = entity.Get<SmallIntestineComponent>();
            if (si.IsEmpty) continue;

            var li   = entity.Get<LargeIntestineComponent>();
            var meta = entity.Get<MetabolismComponent>();

            // How much chyme volume is processed this tick (clamped to available volume).
            float processed = MathF.Min(_cfg.AbsorptionRate * deltaTime, si.CurrentVolumeMl);

            // Proportional batch: the same fraction of contents is processed as
            // fraction of volume drained — consistent with DigestionSystem's model.
            float ratio = processed / si.CurrentVolumeMl;
            NutrientProfile batch = si.Contents * ratio;

            // ── Compute absorbed vs residue per nutrient class ────────────────
            //
            // absorbed  = the fraction of each nutrient that enters the bloodstream
            // residue   = batch - absorbed = what passes onward to the large intestine
            //
            // Fiber is a special case: it contributes 0 to absorbed (no digestive
            // enzyme can break the beta-glycosidic bonds in dietary fiber). We set
            // absorbed.Fiber = 0 explicitly and restore the full batch.Fiber to residue
            // after the subtraction, since the subtraction operator would compute
            // (batch.Fiber - 0) = batch.Fiber automatically. We spell it out for clarity.

            var absorbed = new NutrientProfile
            {
                Carbohydrates = batch.Carbohydrates * _cfg.CarbohydrateAbsorptionFraction,
                Proteins      = batch.Proteins      * _cfg.ProteinAbsorptionFraction,
                Fats          = batch.Fats          * _cfg.FatAbsorptionFraction,
                Fiber         = 0f,   // indigestible — passes entirely to colon
                Water         = batch.Water         * _cfg.WaterAbsorptionFraction,

                // Fat-soluble vitamins (A, D, E, K) are packaged with fat in micelles
                // before absorption. They use the same configurable fraction.
                VitaminA = batch.VitaminA * _cfg.FatSolubleVitaminAbsorptionFraction,
                VitaminD = batch.VitaminD * _cfg.FatSolubleVitaminAbsorptionFraction,
                VitaminE = batch.VitaminE * _cfg.FatSolubleVitaminAbsorptionFraction,
                VitaminK = batch.VitaminK * _cfg.FatSolubleVitaminAbsorptionFraction,

                // Water-soluble vitamins (B-complex, C) absorb readily in the jejunum
                // via carrier-mediated transport and diffusion.
                VitaminB = batch.VitaminB * _cfg.WaterSolubleVitaminAbsorptionFraction,
                VitaminC = batch.VitaminC * _cfg.WaterSolubleVitaminAbsorptionFraction,

                // Minerals: a single average fraction covers all five in v0.7.1.
                // In v0.8, each mineral can get its own absorption fraction if the
                // deficiency system needs accurate per-mineral bioavailability.
                Sodium    = batch.Sodium    * _cfg.MineralAbsorptionFraction,
                Potassium = batch.Potassium * _cfg.MineralAbsorptionFraction,
                Calcium   = batch.Calcium   * _cfg.MineralAbsorptionFraction,
                Iron      = batch.Iron      * _cfg.MineralAbsorptionFraction,
                Magnesium = batch.Magnesium * _cfg.MineralAbsorptionFraction,
            };

            // Residue = everything the SI could not absorb. The subtraction operator
            // on NutrientProfile computes (batch - absorbed) field-by-field. Fiber
            // becomes (batch.Fiber - 0) = batch.Fiber, which is correct.
            NutrientProfile residue = batch - absorbed;

            // ── Drain the SI contents ─────────────────────────────────────────
            si.CurrentVolumeMl -= processed;
            si.Contents        -= batch;

            // Numerical hygiene: floating-point drift on the last tick before empty
            // can produce a tiny negative. Clamp it.
            if (si.CurrentVolumeMl < 0f) si.CurrentVolumeMl = 0f;

            // ── Write absorbed nutrients into body stores ──────────────────────
            // NutrientStores is the real-biology pool. SmallIntestineSystem is the
            // primary writer — DigestionSystem only sets Satiation/Hydration gameplay
            // metrics from the stomach release; it no longer writes to NutrientStores
            // directly (that was the v0.7.0 shortcut that this system replaces).
            meta.NutrientStores += absorbed;

            // ── Forward residue to the large intestine ────────────────────────
            // Residue volume: the portion of the processed volume that wasn't absorbed.
            // Most of the volume in chyme is water; we approximate residue volume as
            // a configured fraction of the processed volume. This avoids needing to
            // model solid vs liquid mass separately.
            float residueVolume = processed * _cfg.ResidueVolumeFraction;

            li.CurrentVolumeMl += residueVolume;
            li.Contents        += residue;

            // Guard LI at capacity. In practice the LI is large enough that overflow
            // should not occur under normal simulation parameters.
            if (li.CurrentVolumeMl > LargeIntestineComponent.CapacityMl)
                li.CurrentVolumeMl = LargeIntestineComponent.CapacityMl;

            entity.Add(si);
            entity.Add(li);
            entity.Add(meta);
        }
    }
}
