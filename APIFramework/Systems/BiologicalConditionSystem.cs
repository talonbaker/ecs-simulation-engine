using APIFramework.Components;
using APIFramework.Core;


// Only spawn water if:
// 1. Human is Thirsty
// 2. Human is NOT already swallowing (Esophagus is clear)
// 3. Human DOES NOT already have a "Water Desire" being processed

public class BiologicalConditionSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        // 1. GLOBAL PAUSE CHECK
        // This prevents the "Infinite Scroll" bug when time is 0
        if (deltaTime <= 0) return;

        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            var meta = entity.Get<MetabolismComponent>();

            // 2. CONSOLIDATED CONCURRENCY CHECK
            // We look for ANY entity in the world where the Target is THIS human
            bool throatIsBusy = em.Query<EsophagusTransitComponent>()
                .Any(t => t.Get<EsophagusTransitComponent>().TargetEntityId == entity.Id);

            // 3. BIOLOGICAL TAG MANAGEMENT
            // These just flip the UI badges
            ToggleTag<ThirstTag>(entity, meta.Thirst >= 70f);
            ToggleTag<DehydratedTag>(entity, meta.Thirst >= 105f);
            ToggleTag<HungerTag>(entity, meta.Hunger >= 75f);
            ToggleTag<StarvingTag>(entity, meta.Hunger >= 105f);
            ToggleTag<IrritableTag>(entity, meta.Hunger > 90f || meta.Thirst > 90f);

            // 4. SEQUENTIAL ACTION LOGIC
            // We only create water if they are thirsty AND the throat is clear
            if (entity.Has<ThirstTag>() && !throatIsBusy)
            {
                var water = em.CreateEntity();
                water.Add(new IdentityComponent("Water", "Liquid"));

                // Lowered to 15f for more realistic, repeated gulps
                water.Add(new LiquidComponent { HydrationValue = 15f, LiquidType = "Water" });

                water.Add(new EsophagusTransitComponent
                {
                    Progress = 0f,
                    Speed = 0.8f, // Water is fast!
                    TargetEntityId = entity.Id
                });
            }
        }
    }

    private void ToggleTag<T>(Entity entity, bool condition) where T : struct
    {
        if (condition && !entity.Has<T>()) entity.Add(new T());
        else if (!condition && entity.Has<T>()) entity.Remove<T>();
    }
}