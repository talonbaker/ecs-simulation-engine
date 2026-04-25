namespace Warden.Orchestrator.Dispatcher;

/// <summary>
/// Bounds the number of concurrently-executing Sonnet calls to <see cref="MaxConcurrentSonnets"/>.
/// Uses <c>SemaphoreSlim(5)</c> + <c>Task.WhenAll</c> as described in SRD §2 Pillar B.
/// </summary>
public sealed class ConcurrencyController
{
    public const int MaxConcurrentSonnets = 5;

    private readonly SemaphoreSlim _semaphore = new(MaxConcurrentSonnets, MaxConcurrentSonnets);

    /// <summary>
    /// Runs all <paramref name="work"/> items concurrently, capping in-flight count at
    /// <see cref="MaxConcurrentSonnets"/>. Returns results in input order.
    /// </summary>
    public async Task<T[]> RunAllAsync<T>(
        IReadOnlyList<Func<CancellationToken, Task<T>>> work,
        IProgress<SonnetProgress>?                      progress,
        CancellationToken                               ct)
    {
        var tasks = work
            .Select(w => RunOneSemaphoredAsync(w, progress, ct))
            .ToArray();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>Current number of available (non-acquired) semaphore slots — for testing.</summary>
    internal int AvailableSlots => _semaphore.CurrentCount;

    private async Task<T> RunOneSemaphoredAsync<T>(
        Func<CancellationToken, Task<T>> work,
        IProgress<SonnetProgress>?       progress,
        CancellationToken                ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await work(ct).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
