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
        // Advance the clock and get the game-seconds elapsed this tick.
        // Systems always receive game-seconds so their math stays independent of TimeScale.
        float scaledDelta = Clock.Tick(realDeltaTime);

        foreach (var system in Systems)
        {
            system.Update(EntityManager, scaledDelta);
        }
    }
}