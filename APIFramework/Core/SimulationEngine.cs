namespace APIFramework.Core;

public class SimulationEngine
{
    private readonly List<ISystem> _systems = new();
    private readonly EntityManager _entityManager;
    private readonly SimulationClock _clock;

    // We inject the manager and clock so they can be shared/monitored
    public SimulationEngine(EntityManager entityManager, SimulationClock clock)
    {
        _entityManager = entityManager;
        _clock = clock;
    }

    public void AddSystem(ISystem system) => _systems.Add(system);

    public void Update(float realDeltaTime)
    {
        // 1. Update the clock with the scaled time
        _clock.DeltaTime = realDeltaTime * _clock.TimeScale;
        _clock.TotalTime += _clock.DeltaTime;

        // 2. Run every system in the order they were added
        foreach (var system in _systems)
        {
            system.Update(_entityManager, _clock.DeltaTime);
        }
    }
}