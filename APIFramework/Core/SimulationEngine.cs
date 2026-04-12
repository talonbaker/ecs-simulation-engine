namespace APIFramework.Core;

public class SimulationEngine
{
    private readonly List<ISystem> _systems = new();

    // Change this to a public property with a private setter
    public EntityManager Manager { get; private set; }

    private readonly SimulationClock _clock;

    public SimulationEngine(EntityManager entityManager, SimulationClock clock)
    {
        Manager = entityManager; // Assign it here
        _clock = clock;
    }

    public void AddSystem(ISystem system) => _systems.Add(system);

    public void Update(float realDeltaTime)
    {
        _clock.DeltaTime = realDeltaTime * _clock.TimeScale;
        _clock.TotalTime += _clock.DeltaTime;

        foreach (var system in _systems)
        {
            system.Update(Manager, _clock.DeltaTime);
        }
    }
}