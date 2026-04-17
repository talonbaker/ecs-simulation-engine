using APIFramework.Core;
using APIFramework.Components;

namespace APIFramework.Systems;

public class MetabolismSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<MetabolismComponent>())
        {
            var meta = entity.Get<MetabolismComponent>();

            // While sleeping (SleepingTag present) metabolism slows dramatically.
            // Breathing and insensible water loss continue but urination/activity stop.
            // SleepMetabolismMultiplier is typically 0.10 — 10% of the awake drain rate.
            float sleepMult = entity.Has<SleepingTag>()
                ? meta.SleepMetabolismMultiplier
                : 1.0f;

            // Drain the physiological resources over time.
            // Hunger and Thirst are computed from these — they rise automatically
            // as Satiation and Hydration fall.
            meta.Satiation = MathF.Max(0f, meta.Satiation - meta.SatiationDrainRate * sleepMult * deltaTime);
            meta.Hydration = MathF.Max(0f, meta.Hydration - meta.HydrationDrainRate * sleepMult * deltaTime);

            entity.Add(meta);
        }
    }
}
