using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Extracts remaining water from intestinal contents and compacts the residue
/// into waste ready for the rectum.
///
/// PIPELINE POSITION
/// ─────────────────
///   SmallIntestineSystem → LargeIntestineComponent → LargeIntestineSystem
///                        → MetabolismComponent.NutrientStores (recaptured water)
///                        → LargeIntestineComponent.WasteReadyMl
///                        → (v0.7.3) RectumComponent
///
/// BIOLOGY
/// ───────
/// The large intestine (colon) receives roughly 1.5 L/day of semi-liquid chyme
/// from the small intestine. Its primary job is not absorption of nutrients —
/// that happened upstream — but water reclamation. The colon wall absorbs ~90%
/// of the remaining water back into the bloodstream, leaving a compact ~150 ml
/// of formed stool per day.
///
/// No macronutrients or vitamins are meaningfully absorbed here. The colon's
/// mucosa is not structured for macromolecular absorption; its microvilli are
/// optimized for water and electrolyte (sodium/potassium) exchange. We note
/// this in the biology layer: LargeIntestineSystem only writes water to
/// NutrientStores; the mineral fraction absorbed here is negligible and omitted
/// for simplicity.
///
/// TRANSIT RATE
/// ─────────────
/// Real colonic transit time is 12–36 hours. At the default 120× timescale:
///   - 7.5 ml residue (from one banana's 15% residue fraction of 50 ml)
///     clears at WaterExtractionRate = 0.002 ml/game-s in:
///     7.5 / 0.002 = 3,750 game-s ≈ 1.04 game-hours ≈ 52 real-seconds at 120×
///
/// This is faster than biological reality but gives observable LI transit in a
/// single session. Tune WaterExtractionRate in SimConfig.json if you want slower
/// (more realistic) or faster (more dramatized) colon transit.
///
/// WASTE ACCUMULATION (v0.7.3 FORWARD COMPAT)
/// ────────────────────────────────────────────
/// WasteReadyMl accumulates as water is extracted. In v0.7.3 this field feeds
/// RectumComponent: when WasteReadyMl exceeds LargeIntestineSystemConfig.TransitThresholdMl,
/// LargeIntestineSystem will transfer the accumulated waste into RectumComponent,
/// triggering the defecation desire pipeline. Until v0.7.3 lands, the field grows
/// without being drained — intentionally, because waste must accumulate somewhere.
/// The volumes are small enough (15% residue × typical meal volume) that overflow
/// is not a concern in normal simulation runs.
/// </summary>
public class LargeIntestineSystem : ISystem
{
    private readonly LargeIntestineSystemConfig _cfg;

    public LargeIntestineSystem(LargeIntestineSystemConfig cfg)
    {
        _cfg = cfg;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<LargeIntestineComponent>().ToList())
        {
            if (!entity.Has<MetabolismComponent>()) continue;

            var li = entity.Get<LargeIntestineComponent>();
            if (li.IsEmpty) continue;

            var meta = entity.Get<MetabolismComponent>();

            // How much content is desiccated (processed) this tick.
            float processed = MathF.Min(_cfg.WaterExtractionRate * deltaTime, li.CurrentVolumeMl);
            float ratio     = processed / li.CurrentVolumeMl;

            // The batch of contents being processed this tick.
            NutrientProfile batch = li.Contents * ratio;

            // ── Water recapture ────────────────────────────────────────────────
            // The colon's primary function: recover water from the passing chyme.
            // Recovered water goes to NutrientStores.Water — the biology-layer pool
            // that BodyMetabolismSystem will drain in v0.8+.
            //
            // IMPORTANT: we do NOT add to Hydration here. DigestionSystem already
            // applied the Hydration gameplay metric at the stomach stage. If we added
            // it again here we would be double-counting — Billy would get two
            // hydration bumps from the same gulp of water. NutrientStores.Water is
            // the biology layer; Hydration (0–100) is the gameplay layer; they are
            // intentionally separate.
            float waterRecaptured = batch.Water * _cfg.WaterRecaptureFraction;

            // Build a water-only profile and add it to stores via the operator
            // overload, consistent with how the rest of the pipeline updates NutrientStores.
            var recapturedProfile = new NutrientProfile { Water = waterRecaptured };
            meta.NutrientStores += recapturedProfile;

            // ── Waste compaction ───────────────────────────────────────────────
            // Everything not absorbed (the unrecaptured water fraction + fiber +
            // unabsorbed minerals) becomes compacted stool. We approximate waste
            // volume as the processed volume minus the fraction absorbed as water.
            // The (1 - WaterRecaptureFraction) factor represents the moisture retained
            // in the stool and the solid mass of fiber and mineral residue.
            li.WasteReadyMl += processed * (1f - _cfg.WaterRecaptureFraction);

            // ── Drain the LI contents ──────────────────────────────────────────
            li.CurrentVolumeMl -= processed;
            li.Contents        -= batch;

            if (li.CurrentVolumeMl < 0f) li.CurrentVolumeMl = 0f;

            entity.Add(li);
            entity.Add(meta);
        }
    }
}
