using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

public class DesireSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        if (deltaTime <= 0) return; // Stop if the simulation is paused!

        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            // 1. Evaluate Biological Needs
            bool needsWater = entity.Has<ThirstTag>();
            bool needsFood = entity.Has<HungerTag>();

            // 2. Map Needs to Desires
            // If they are hungry AND don't already have a banana in hand...
            if (needsFood && !entity.Has<FoodObjectComponent>())
            {
                if (!entity.Has<FoodDesireTag>()) entity.Add(new FoodDesireTag());
            }
            else
            {
                entity.Remove<FoodDesireTag>();
            }

            // Same for water
            if (needsWater && !entity.Has<WaterDesireTag>())
            {
                if (!entity.Has<WaterDesireTag>()) entity.Add(new WaterDesireTag());
            }
        }
    }

    private void ToggleDesire<T>(Entity entity, bool condition) where T : struct
    {
        if (condition && !entity.Has<T>()) entity.Add(new T());
        else if (!condition && entity.Has<T>()) entity.Remove<T>();
    }
}