using APIFramework.Components;
using APIFramework.Core;

public class EsophagusSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        var entities = em.Query<EsophagusTransitComponent>().ToList();

        foreach (var entity in entities)
        {
            var transit = entity.Get<EsophagusTransitComponent>();
            transit.Progress += transit.Speed * deltaTime;

            if (transit.Progress >= 1.0f)
            {
                em.DestroyEntity(entity);
            }
            else
            {
                entity.Add(transit);
            }
        }
    }
}