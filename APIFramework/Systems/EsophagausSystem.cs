using APIFramework.Components;
using APIFramework.Core;

public class EsophagusSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<EsophagusTransitComponent>())
        {
            var transit = entity.Get<EsophagusTransitComponent>();

            // Move the bolus down
            transit.Progress += transit.Speed * deltaTime;

            if (transit.Progress >= 1.0f)
            {
                // It reached the stomach! 
                // For now, let's just destroy the entity or move it to a 'Stomach' component
                em.DestroyEntity(entity);
            }
            else
            {
                entity.Add(transit);
            }
        }
    }
}