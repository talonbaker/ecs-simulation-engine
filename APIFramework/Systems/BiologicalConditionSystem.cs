using APIFramework.Components;
using APIFramework.Core;

public class BiologicalConditionSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            var meta = entity.Get<MetabolismComponent>();

            bool isEsophagusBusy = em.Query<EsophagusTransitComponent>()
                             .Any(t => t.Get<EsophagusTransitComponent>().TargetEntityId == entity.Id);

            // DEBUG: Check if the system is alive
            // Console.WriteLine($"System checking {entity.ShortId}: T={meta.Thirst}");

            ToggleTag<ThirstTag>(entity, meta.Thirst >= 70f);
            ToggleTag<DehydratedTag>(entity, meta.Thirst >= 105f);
            ToggleTag<HungerTag>(entity, meta.Hunger >= 75f);
            ToggleTag<StarvingTag>(entity, meta.Hunger >= 105f);

            bool isUpset = (meta.Hunger > 90f || meta.Thirst > 90f);
            ToggleTag<IrritableTag>(entity, isUpset);

            // Inside BiologicalConditionSystem.Update
            if (entity.Has<ThirstTag>() && !entity.Has<EsophagusTransitComponent>())
            {
                // Create the "Water" entity
                var water = em.CreateEntity();
                water.Add(new IdentityComponent("Water", "Liquid"));
                water.Add(new LiquidComponent { HydrationValue = 50f, LiquidType = "Water" });

                // Attach it to the esophagus system
                water.Add(new EsophagusTransitComponent
                {
                    Progress = 0f,
                    Speed = 0.5f, // Water flows faster than a bolus!
                    TargetEntityId = entity.Id
                });
            }
        }
    }

    // A small helper to keep the code clean
    private void ToggleTag<T>(Entity entity, bool condition) where T : struct
    {
        if (condition && !entity.Has<T>()) entity.Add(new T());
        else if (!condition && entity.Has<T>()) entity.Remove<T>();
    }
}