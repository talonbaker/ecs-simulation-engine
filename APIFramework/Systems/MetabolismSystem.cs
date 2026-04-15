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

            // Drain the physiological resources over time.
            // Hunger and Thirst are computed from these — they rise automatically
            // as Satiation and Hydration fall.
            meta.Satiation = MathF.Max(0f, meta.Satiation - meta.SatiationDrainRate * deltaTime);
            meta.Hydration = MathF.Max(0f, meta.Hydration - meta.HydrationDrainRate * deltaTime);

            entity.Add(meta);
        }
    }
}
