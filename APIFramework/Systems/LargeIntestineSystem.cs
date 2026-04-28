using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Water reabsorption and stool formation in the large intestine.
///
/// PIPELINE POSITION
/// ─────────────────
///   SmallIntestineSystem → LargeIntestineSystem → ColonComponent
///
/// WHAT HAPPENS HERE
/// ─────────────────
///   1. Water reabsorption: a small amount of water is recovered from LI content
///      and added to MetabolismComponent.Hydration each tick. This is the secondary
///      hydration source — slower and smaller than drinking, but meaningful over time.
///      Only runs while content is present (dry LI does not reabsorb anything).
///
///   2. Stool formation: content advances toward the colon at MobilityRate.
///      StoolFraction of the processed volume arrives in ColonComponent as formed stool.
///      The remainder (1 - StoolFraction) is water and gas that dissipates silently.
///
/// PER TICK
/// ─────────
///   waterGain       = WaterReabsorptionRate × deltaTime  → meta.Hydration
///   processed       = min(MobilityRate × deltaTime, ContentVolumeMl)
///   ContentVolumeMl -= processed
///   stool           = processed × StoolFraction          → colon.StoolVolumeMl
/// </summary>
public class LargeIntestineSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<LargeIntestineComponent>().ToList())
        {
            if (!LifeStateGuard.IsBiologicallyTicking(entity)) continue;  // WP-3.0.0: skip Deceased NPCs (Incapacitated still ticks)

            var li = entity.Get<LargeIntestineComponent>();

            // ── 1. Water reabsorption ─────────────────────────────────────────
            // Only absorb when content is present; a fully empty LI has nothing to squeeze.
            if (!li.IsEmpty && entity.Has<MetabolismComponent>())
            {
                var meta       = entity.Get<MetabolismComponent>();
                meta.Hydration = MathF.Min(100f, meta.Hydration + li.WaterReabsorptionRate * deltaTime);
                entity.Add(meta);
            }

            // ── 2. Content mobility → stool formation ────────────────────────
            if (li.IsEmpty)
            {
                entity.Add(li);
                continue;
            }

            float processed    = MathF.Min(li.MobilityRate * deltaTime, li.ContentVolumeMl);
            li.ContentVolumeMl -= processed;
            if (li.ContentVolumeMl < 0f) li.ContentVolumeMl = 0f;

            entity.Add(li);

            if (!entity.Has<ColonComponent>()) continue;

            var colon           = entity.Get<ColonComponent>();
            float stool         = processed * li.StoolFraction;
            // Colon can overflow if LI produces stool faster than it empties — cap at capacity.
            colon.StoolVolumeMl = MathF.Min(colon.CapacityMl, colon.StoolVolumeMl + stool);
            entity.Add(colon);
        }
    }
}
