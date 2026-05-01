using Warden.Contracts.Handshake;
using Warden.Contracts.Telemetry;
using Warden.Orchestrator.Batch;

namespace Warden.Orchestrator.Dispatcher;

/// <summary>
/// Thin wrapper over <see cref="BatchScheduler.RunAsync"/>.
/// Responsible for enforcing the 25-scenario cap before delegating.
/// </summary>
public sealed class HaikuDispatcher
{
    private readonly BatchScheduler _scheduler;

    public HaikuDispatcher(BatchScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    /// <summary>
    /// Submits all pending scenario batches to the Haiku tier.
    /// At most 25 total scenarios are allowed across all batches.
    /// </summary>
    public Task<IReadOnlyList<HaikuResult>> RunAsync(
        string runId,
        IReadOnlyList<ScenarioBatch> batches,
        CancellationToken ct,
        WorldStateDto? state          = null,
        bool           spatialContext = false)
        => _scheduler.RunAsync(runId, batches, ct, state, spatialContext);
}
