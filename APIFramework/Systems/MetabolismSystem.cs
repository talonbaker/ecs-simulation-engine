using APIFramework.Core;
using APIFramework.Components;

namespace APIFramework.Systems;

public class MetabolismSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<MetabolismComponent>())
        {
            // 1. This creates a COPY of the data
            var meta = entity.Get<MetabolismComponent>();

            // 2. You modify the COPY
            meta.Hunger += meta.HungerRate * deltaTime;

            // 3. YOU MUST OVERWRITE the original data with the updated copy
            entity.Add(meta);
        }
    }
}