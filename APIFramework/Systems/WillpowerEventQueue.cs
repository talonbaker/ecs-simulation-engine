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
    private IReadOnlyList<WillpowerEventSignal> _lastDrainedBatch = System.Array.Empty<WillpowerEventSignal>();

    /// <summary>Pushes a willpower-change signal onto the queue. Thread-safe.</summary>
    /// <param name="signal">The suppression-tick or rest-tick signal to enqueue.</param>
    public void Enqueue(WillpowerEventSignal signal) => _queue.Enqueue(signal);

    /// <summary>
    /// The batch of signals returned by the most recent <see cref="DrainAll"/> call.
    /// StressSystem (running at Cleanup phase, after WillpowerSystem) reads this to count
    /// suppression events processed this tick. Empty until WillpowerSystem runs.
    /// </summary>
    public IReadOnlyList<WillpowerEventSignal> LastDrainedBatch => _lastDrainedBatch;

    /// <summary>Removes and returns all pending signals. Called once per tick by WillpowerSystem.</summary>
    /// <returns>All signals queued since the previous drain; never null.</returns>
    public List<WillpowerEventSignal> DrainAll()
    {
        var result = new List<WillpowerEventSignal>();
        while (_queue.TryDequeue(out var sig))
            result.Add(sig);
        _lastDrainedBatch = result;
        return result;
    }
}
