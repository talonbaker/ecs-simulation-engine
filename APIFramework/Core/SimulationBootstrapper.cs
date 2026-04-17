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
///
/// SYSTEM PIPELINE (execution order)
/// ──────────────────────────────────
///  1. MetabolismSystem         — drain satiation / hydration
///  2. EnergySystem             — drain/restore energy + sleepiness; energy-state tags
///  3. BiologicalConditionSystem — set hunger/thirst/irritable tags
///  4. BrainSystem              — score all drives (incl. circadian sleep); pick dominant
///  5. FeedingSystem            — act if Eat is dominant
///  6. DrinkingSystem           — act if Drink is dominant
///  7. SleepSystem              — toggle IsSleeping based on dominant desire
///  8. InteractionSystem        — convert held food to esophagus bolus
///  9. EsophagusSystem          — move transit entities toward stomach
/// 10. DigestionSystem          — release nutrients from stomach to metabolism
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
        Clock         = new SimulationClock { TimeScale = Config.World.DefaultTimeScale };
        Engine        = new SimulationEngine(EntityManager, Clock);

        RegisterSystems();
        SpawnWorld();
    }

    private void RegisterSystems()
    {
        var sys = Config.Systems;

        Engine.AddSystem(new MetabolismSystem());                                  //  1
        Engine.AddSystem(new EnergySystem(sys.Energy));                           //  2
        Engine.AddSystem(new BiologicalConditionSystem(sys.BiologicalCondition)); //  3
        Engine.AddSystem(new BrainSystem(sys.Brain, Clock));                      //  4
        Engine.AddSystem(new FeedingSystem(sys.Feeding));                         //  5
        Engine.AddSystem(new DrinkingSystem(sys.Drinking));                       //  6
        Engine.AddSystem(new SleepSystem(sys.Sleep));                             //  7
        Engine.AddSystem(new InteractionSystem(sys.Interaction));                 //  8
        Engine.AddSystem(new EsophagusSystem());                                  //  9
        Engine.AddSystem(new DigestionSystem());                                  // 10
    }

    private void SpawnWorld()
    {
        EntityTemplates.SpawnHuman(EntityManager, Config.Entities.Human);
    }
}
