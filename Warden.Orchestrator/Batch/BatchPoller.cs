using Warden.Anthropic;

namespace Warden.Orchestrator.Batch;

/// <summary>
/// Timer-based poller for the Message Batches API.
/// Default interval: 60s. Exponential backoff to 5min on consecutive no-change polls.
/// </summary>
public sealed class BatchPoller
{
    internal static readonly TimeSpan BaseInterval = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan MaxInterval  = TimeSpan.FromMinutes(5);

    private readonly Func<TimeSpan, CancellationToken, Task> _sleep;

    /// <param name="sleep">
    /// Optional override for the sleep function. Defaults to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
    /// Inject a no-op or recording function in tests.
    /// </param>
    public BatchPoller(Func<TimeSpan, CancellationToken, Task>? sleep = null)
        => _sleep = sleep ?? ((d, ct) => Task.Delay(d, ct));

    /// <summary>
    /// Polls until <c>processing_status == "ended"</c>, respecting <paramref name="ct"/>.
    /// Sleeps before each poll so the first check is after one interval.
    /// </summary>
    public async Task PollUntilEndedAsync(
        AnthropicClient client,
        string          batchId,
        CancellationToken ct)
    {
        int consecutiveNoChange        = 0;
        BatchRequestCounts? lastCounts = null;

        while (true)
        {
            await _sleep(ComputeInterval(consecutiveNoChange), ct).ConfigureAwait(false);

            var status = await client.GetBatchAsync(batchId, ct).ConfigureAwait(false);

            if (status.ProcessingStatus == BatchProcessingStatus.Ended)
                return;

            if (lastCounts is not null && CountsMatch(lastCounts, status.RequestCounts))
                consecutiveNoChange++;
            else
                consecutiveNoChange = 0;

            lastCounts = status.RequestCounts;
        }
    }

    /// <summary>
    /// Returns the sleep interval for a given consecutive-no-change count.
    /// 0 → 60s; doubles each increment; capped at 5min.
    /// </summary>
    internal static TimeSpan ComputeInterval(int consecutiveNoChange)
    {
        if (consecutiveNoChange == 0)
            return BaseInterval;

        var factor = 1L << Math.Min(consecutiveNoChange, 30);
        var ticks  = Math.Min(BaseInterval.Ticks * factor, MaxInterval.Ticks);
        return TimeSpan.FromTicks(ticks);
    }

    private static bool CountsMatch(BatchRequestCounts a, BatchRequestCounts b)
        => a.Processing == b.Processing
        && a.Succeeded  == b.Succeeded
        && a.Errored    == b.Errored
        && a.Canceled   == b.Canceled
        && a.Expired    == b.Expired;
}
