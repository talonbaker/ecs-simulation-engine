using APIFramework.Core;
using APIFramework.Components;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Physiology phase. Drains <see cref="MetabolismComponent"/> Satiation and Hydration
/// each tick. Drain rate is multiplied down during sleep (SleepingTag) and up when
/// the entity is angry or raging (cortisol effect).
/// </summary>
/// <remarks>
/// Reads: <see cref="MetabolismComponent"/>, <see cref="SleepingTag"/>,
/// <see cref="AngryTag"/>, <see cref="RagingTag"/>, <see cref="LifeStateComponent"/>.<br/>
/// Writes: <see cref="MetabolismComponent"/> Satiation and Hydration (single writer of macro-resource drain).<br/>
/// Phase: Physiology — first system in the per-tick biology cycle.
/// </remarks>
public class MetabolismSystem : ISystem
{
    /// <summary>Per-tick metabolic drain pass.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<MetabolismComponent>())
        {
            if (!LifeStateGuard.IsBiologicallyTicking(entity)) continue;  // WP-3.0.0: skip Deceased NPCs (Incapacitated still ticks)

            var meta = entity.Get<MetabolismComponent>();

            // While sleeping (SleepingTag present) metabolism slows dramatically.
            // Breathing and insensible water loss continue but urination/activity stop.
            // SleepMetabolismMultiplier is typically 0.10 — 10% of the awake drain rate.
            float sleepMult = entity.Has<SleepingTag>()
                ? meta.SleepMetabolismMultiplier
                : 1.0f;

            // Anger (stress physiology) raises metabolic drain — cortisol effect.
            // AngryTag or RagingTag both trigger this; the effect is the same intensity.
            float angerMult = (entity.Has<AngryTag>() || entity.Has<RagingTag>())
                ? 1.25f   // 25% faster drain when actively angry
                : 1.0f;

            float totalMult = sleepMult * angerMult;

            // Drain the physiological resources over time.
            meta.Satiation = MathF.Max(0f, meta.Satiation - meta.SatiationDrainRate * totalMult * deltaTime);
            meta.Hydration = MathF.Max(0f, meta.Hydration - meta.HydrationDrainRate * totalMult * deltaTime);

            entity.Add(meta);
        }
    }
}
