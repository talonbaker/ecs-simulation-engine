using Warden.Orchestrator.Dispatcher;
using Xunit;

namespace Warden.Orchestrator.Tests.Dispatcher;

public sealed class ConcurrencyControllerTests
{
    // -- AT-02 ---------------------------------------------------------------------

    [Fact]
    public async Task AT02_MaxInFlight_Never_Exceeds_5_For_10_Concurrent_Specs()
    {
        var controller   = new ConcurrencyController();
        int currentCount = 0;
        int maxObserved  = 0;
        var gate         = new object();
        var tcs          = new TaskCompletionSource<bool>();

        // Create 10 work items. Each increments currentCount while "in-flight"
        // (holding the semaphore), records the max, then yields long enough that
        // other tasks can also be in-flight.
        var work = Enumerable.Range(0, 10)
            .Select(_ => new Func<CancellationToken, Task<int>>(async ct =>
            {
                lock (gate)
                {
                    currentCount++;
                    if (currentCount > maxObserved) maxObserved = currentCount;
                }
                await Task.Delay(30, ct);
                lock (gate) { currentCount--; }
                return 0;
            }))
            .ToList();

        var results = await controller.RunAllAsync(work, null, CancellationToken.None);

        Assert.Equal(10, results.Length);
        Assert.True(maxObserved <= ConcurrencyController.MaxConcurrentSonnets,
            $"Max concurrent in-flight was {maxObserved}; expected ≤ {ConcurrencyController.MaxConcurrentSonnets}.");
        Assert.True(maxObserved > 0, "At least one task must have been in flight.");
    }

    [Fact]
    public async Task RunAllAsync_ReturnsResultsInInputOrder()
    {
        var controller = new ConcurrencyController();
        var order      = new int[5];
        var delays     = new[] { 50, 10, 30, 5, 20 }; // out-of-completion-order

        var work = delays.Select((d, i) => new Func<CancellationToken, Task<int>>(async ct =>
        {
            await Task.Delay(d, ct);
            return i;
        })).ToList();

        var results = await controller.RunAllAsync(work, null, CancellationToken.None);

        // Despite different completion times, Task.WhenAll preserves input order
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, results);
    }

    [Fact]
    public async Task RunAllAsync_CancellationPropagated()
    {
        var controller = new ConcurrencyController();
        var cts        = new CancellationTokenSource();

        var work = Enumerable.Range(0, 3)
            .Select(_ => new Func<CancellationToken, Task<int>>(async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return 0;
            }))
            .ToList();

        cts.CancelAfter(100);
        var ex = await Record.ExceptionAsync(
            () => controller.RunAllAsync(work, null, cts.Token));

        Assert.True(ex is OperationCanceledException,
            $"Expected OperationCanceledException; got {ex?.GetType().Name ?? "null"}.");
    }

    // -- AT-03 ---------------------------------------------------------------------

    [Fact]
    public async Task AT03_RetryPolicy_Retries_429_Up_To_3_Times_Then_Raises()
    {
        var pipeline = RetryPolicy.Build(TimeSpan.Zero);
        int attempts = 0;

        var ex = await Assert.ThrowsAsync<Warden.Anthropic.AnthropicApiException>(async () =>
            await pipeline.ExecuteAsync<Warden.Anthropic.MessageResponse>(async ct =>
            {
                attempts++;
                throw new Warden.Anthropic.AnthropicApiException(
                    System.Net.HttpStatusCode.TooManyRequests, "rate limited");
#pragma warning disable CS0162
                return null!;
#pragma warning restore CS0162
            }, CancellationToken.None));

        Assert.Equal(4, attempts); // 1 initial + 3 retries
        Assert.True(ex.IsRetryable);
    }

    [Fact]
    public async Task AT03_RetryPolicy_Does_Not_Retry_400()
    {
        var pipeline = RetryPolicy.Build(TimeSpan.Zero);
        int attempts = 0;

        await Assert.ThrowsAsync<Warden.Anthropic.AnthropicApiException>(async () =>
            await pipeline.ExecuteAsync<Warden.Anthropic.MessageResponse>(async ct =>
            {
                attempts++;
                throw new Warden.Anthropic.AnthropicApiException(
                    System.Net.HttpStatusCode.BadRequest, "bad request");
#pragma warning disable CS0162
                return null!;
#pragma warning restore CS0162
            }, CancellationToken.None));

        Assert.Equal(1, attempts); // No retries for 400
    }
}
