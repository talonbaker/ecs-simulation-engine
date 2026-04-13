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

            // No more hard-coded 0.2f!
            meta.Hunger += meta.HungerRate * deltaTime;
            meta.Thirst += meta.ThirstRate * deltaTime;

            entity.Add(meta);
        }
    }
}