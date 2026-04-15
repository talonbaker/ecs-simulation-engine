using APIFramework.Components;
using APIFramework.Systems;

namespace APIFramework.Core;

/// <summary>
/// Wires the simulation up completely with no UI dependency.
/// Any frontend — Avalonia, Unity, console — creates one of these,
/// then calls Engine.Update(deltaTime) inside its own loop.
/// APIFramework never knows a UI exists.
/// </summary>
public class SimulationBootstrapper
{
    public SimulationEngine Engine       { get; }
    public EntityManager    EntityManager { get; }
    public SimulationClock  Clock        { get; }

    public SimulationBootstrapper()
    {
        EntityManager = new EntityManager();
        Clock         = new SimulationClock { TimeScale = 1.0f };
        Engine        = new SimulationEngine(EntityManager, Clock);

        // Systems run in this order every tick — order matters
        Engine.AddSystem(new MetabolismSystem());           // 1. Drives hunger/thirst up over time
        Engine.AddSystem(new BiologicalConditionSystem());  // 2. Sets/clears biological state tags
        Engine.AddSystem(new DesireSystem());               // 3. Maps biological tags → desire tags
        Engine.AddSystem(new FeedingSystem());              // 4. Hardcoded food source — spawns bolus when hungry
        Engine.AddSystem(new InteractionSystem());          // 5. Handles held food / bite logic
        Engine.AddSystem(new EsophagusSystem());            // 6. Moves bolus/liquid down the esophagus
        Engine.AddSystem(new DigestionSystem());            // 7. Drains stomach → releases nutrients to metabolism

        // Spawn initial world entities
        EntityTemplates.SpawnHuman(EntityManager);
    }
}
