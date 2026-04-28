using APIFramework.Core;
using APIFramework.Components;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

public class MetabolismSystem : ISystem
{
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
