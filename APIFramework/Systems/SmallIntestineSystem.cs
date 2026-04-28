using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Drains chyme from the small intestine and hands residue off to the large intestine.
///
/// PIPELINE POSITION
/// ─────────────────
///   DigestionSystem (Transit/50) deposits chyme here via ResidueFraction.
///   SmallIntestineSystem (Elimination/55) drains it one tick later.
///   Output → LargeIntestineComponent (indigestible residue volume)
///
/// DESIGN NOTE — nutrient absorption
/// ──────────────────────────────────
/// DigestionSystem already converts stomach contents into Satiation / Hydration /
/// NutrientStores. SmallIntestineSystem does NOT re-absorb nutrients; it is purely
/// a volume-routing system that models the physical transit of indigestible matter.
/// SmallIntestineComponent.Chyme tracks the nutrient profile of chyme in transit for
/// display purposes; it is decremented proportionally as volume drains.
///
/// PER TICK
/// ─────────
///   processed = min(AbsorptionRate × deltaTime, ChymeVolumeMl)
///   ratio     = processed / ChymeVolumeMl
///   Chyme    -= Chyme × ratio   (proportional drain)
///   ChymeVolumeMl -= processed
///   residue   = processed × ResidueToLargeFraction → LargeIntestineComponent
/// </summary>
public class SmallIntestineSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<SmallIntestineComponent>().ToList())
        {
            if (!LifeStateGuard.IsBiologicallyTicking(entity)) continue;  // WP-3.0.0: skip Deceased NPCs (Incapacitated still ticks)

            var si = entity.Get<SmallIntestineComponent>();
            if (si.IsEmpty) continue;

            float processed = MathF.Min(si.AbsorptionRate * deltaTime, si.ChymeVolumeMl);
            float ratio     = processed / si.ChymeVolumeMl;

            // Drain the proportional nutrient slice (tracking only — no re-absorption)
            si.Chyme         -= si.Chyme * ratio;
            si.ChymeVolumeMl -= processed;
            if (si.ChymeVolumeMl < 0f) si.ChymeVolumeMl = 0f;

            entity.Add(si);

            // Residue handoff to large intestine
            if (!entity.Has<LargeIntestineComponent>()) continue;

            var li      = entity.Get<LargeIntestineComponent>();
            float residue = processed * si.ResidueToLargeFraction;
            li.ContentVolumeMl = MathF.Min(LargeIntestineComponent.MaxVolumeMl,
                                           li.ContentVolumeMl + residue);
            entity.Add(li);
        }
    }
}
