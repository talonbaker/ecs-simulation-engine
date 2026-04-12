using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

public class FeedingSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<MetabolismComponent>())
        {
            var meta = entity.Get<MetabolismComponent>();

            if (meta.Hunger >= 100f)
            {
                // 1. Reset Hunger
                meta.Hunger = 0f;
                entity.Add(meta);

                // 2. Spawn the Food Ball (The Bolus)
                var bolus = em.CreateEntity();
                bolus.Add(new BolusComponent { Volume = 10f });

                // 3. Start the transit process
                bolus.Add(new EsophagusTransitComponent { Progress = 0f, Speed = 0.2f });
            }
        }
    }
}