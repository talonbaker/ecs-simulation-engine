using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

public class FeedingSystem : ISystem
{
    // Inside FeedingSystem.cs
    public void Update(EntityManager em, float deltaTime)
    {
        // Add .ToList() to create a snapshot of the current metabolism entities
        var entities = em.Query<MetabolismComponent>().ToList();

        foreach (var entity in entities)
        {
            var meta = entity.Get<MetabolismComponent>();
            if (meta.Hunger >= 100f)
            {
                meta.Hunger = 0f;
                entity.Add(meta);

                // Now this won't crash the loop!
                var bolus = em.CreateEntity();
                bolus.Add(new IdentityComponent { Name = "Bolus" });
                bolus.Add(new EsophagusTransitComponent { Progress = 0f, Speed = 0.2f });
            }
        }
    }
}