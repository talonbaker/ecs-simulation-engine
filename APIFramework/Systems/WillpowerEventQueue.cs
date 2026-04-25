using System.Collections.Concurrent;
using System.Collections.Generic;

namespace APIFramework.Systems;

/// <summary>
/// Thread-safe queue of willpower events.
/// Producers (action-selection, StressSystem, SleepSystem…) enqueue signals.
/// WillpowerSystem drains the queue each tick on the simulation thread.
/// Registered as a singleton in SimulationBootstrapper so all producers share one instance.
/// </summary>
public class WillpowerEventQueue
{
    private readonly ConcurrentQueue<WillpowerEventSignal> _queue = new();

    public void Enqueue(WillpowerEventSignal signal) => _queue.Enqueue(signal);

    /// <summary>Removes and returns all pending signals. Called once per tick by WillpowerSystem.</summary>
    public List<WillpowerEventSignal> DrainAll()
    {
        var result = new List<WillpowerEventSignal>();
        while (_queue.TryDequeue(out var sig))
            result.Add(sig);
        return result;
    }
}
