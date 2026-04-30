using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Legacy desire-tag mapper that toggles <see cref="FoodDesireTag"/> and
/// <see cref="WaterDesireTag"/> from <see cref="HungerTag"/>/<see cref="ThirstTag"/>.
/// </summary>
/// <remarks>
/// Reads: <see cref="MetabolismComponent"/>, <see cref="HungerTag"/>,
/// <see cref="ThirstTag"/>, <see cref="FoodObjectComponent"/>,
/// <see cref="LifeStateComponent"/>.<br/>
/// Writes: <see cref="FoodDesireTag"/>, <see cref="WaterDesireTag"/>.<br/>
/// Not registered in <see cref="APIFramework.Core.SimulationBootstrapper"/>'s pipeline —
/// kept for legacy/test scaffolding. <see cref="BrainSystem"/> is the production
/// drive-selector.
/// </remarks>
public class DesireSystem : ISystem
{
    /// <summary>Per-tick desire-tag toggle pass. No-op when paused (deltaTime &lt;= 0).</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        if (deltaTime <= 0) return; // Stop if the simulation is paused!

        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
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