namespace APIFramework.Core;

public class SimulationEngine
{
    public List<ISystem> Systems { get; private set; } = new();

    public EntityManager EntityManager { get; private set; }
    public SimulationClock Clock { get; private set; }

    // We inject the manager and clock so they can be shared/monitored
    public SimulationEngine(EntityManager entityManager, SimulationClock clock)
    {
        EntityManager = entityManager;
        Clock = clock;
    }

    public void AddSystem(ISystem system) => Systems.Add(system);

    public void Update(float realDeltaTime)
    {
        // Update the clock with the scaled time
        Clock.DeltaTime = realDeltaTime * Clock.TimeScale;
        Clock.TotalTime += Clock.DeltaTime;

        // Run every system in the order they were added
        foreach (var system in Systems)
        {
            system.Update(EntityManager, Clock.DeltaTime);
        }
    }
}