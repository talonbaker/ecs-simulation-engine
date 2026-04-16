using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Systems;

namespace APIFramework.Core;

/// <summary>
/// Composition root for the entire headless simulation.
/// Loads SimConfig.json, wires every system in execution order, and spawns
/// the initial world entities.
///
/// Any frontend — Avalonia GUI, CLI, Unity, test harness — creates one instance
/// of this class and drives it by calling Engine.Update(deltaTime) in its own loop.
/// APIFramework never knows a frontend exists.
/// </summary>
public class SimulationBootstrapper
{
    public SimulationEngine Engine        { get; }
    public EntityManager    EntityManager { get; }
    public SimulationClock  Clock         { get; }
    public SimConfig        Config        { get; }

    public SimulationBootstrapper(string configPath = "SimConfig.json")
    {
        Config        = SimConfig.Load(configPath);
        EntityManager = new EntityManager();
        Clock         = new SimulationClock { TimeScale = 1.0f };
        Engine        = new SimulationEngine(EntityManager, Clock);

        RegisterSystems();
        SpawnWorld();
    }

    private void RegisterSystems()
    {
        var sys = Config.Systems;

        // ── Execution order is load order. Change it here, nowhere else. ────
        Engine.AddSystem(new MetabolismSystem());                              // 1. Drain resources
        Engine.AddSystem(new BiologicalConditionSystem(sys.BiologicalCondition)); // 2. Set condition tags
        Engine.AddSystem(new BrainSystem(sys.Brain));                         // 3. Score drives, pick dominant
        Engine.AddSystem(new FeedingSystem(sys.Feeding));                     // 4. Act if Eat is dominant
        Engine.AddSystem(new DrinkingSystem(sys.Drinking));                   // 5. Act if Drink is dominant
        Engine.AddSystem(new InteractionSystem(sys.Interaction));             // 6. Process held food (bite → bolus)
        Engine.AddSystem(new EsophagusSystem());                              // 7. Move transit entities
        Engine.AddSystem(new DigestionSystem());                              // 8. Release nutrients to metabolism
    }

    private void SpawnWorld()
    {
        EntityTemplates.SpawnHuman(EntityManager, Config.Entities.Human);
    }
}
