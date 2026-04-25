using Polly;
using Polly.Retry;
using Warden.Anthropic;

namespace Warden.Orchestrator;

/// <summary>
/// Polly resilience pipeline for Anthropic API calls.
/// Retries on 429 and 5xx up to 3 times with exponential backoff starting at 2s.
/// Never retries 4xx (other than 429).
/// </summary>
public static class RetryPolicy
{
    /// <summary>
    /// Builds the production retry pipeline.
    /// </summary>
    /// <param name="baseDelay">
    /// Base delay between retries. Defaults to 2 seconds.
    /// Pass <see cref="TimeSpan.Zero"/> in tests to skip actual delays.
    /// </param>
    public static ResiliencePipeline<MessageResponse> Build(TimeSpan? baseDelay = null)
    {
        return new ResiliencePipelineBuilder<MessageResponse>()
            .AddRetry(new RetryStrategyOptions<MessageResponse>
            {
                MaxRetryAttempts = 3,
                Delay            = baseDelay ?? TimeSpan.FromSeconds(2),
                BackoffType      = DelayBackoffType.Exponential,
                UseJitter        = baseDelay is null,
                ShouldHandle     = new PredicateBuilder<MessageResponse>()
                    .Handle<AnthropicApiException>(ex => ex.IsRetryable)
            })
            .Build();
    }
}
