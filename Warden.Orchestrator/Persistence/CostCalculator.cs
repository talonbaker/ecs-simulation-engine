using Warden.Anthropic;

namespace Warden.Orchestrator.Persistence;

/// <summary>
/// Pure USD-cost computation from an API response's token counters.
/// No state; safe to share across threads.
/// </summary>
public sealed class CostCalculator
{
    /// <summary>
    /// Returns the USD cost for one API call.
    /// </summary>
    /// <param name="model">Model that produced the response.</param>
    /// <param name="usage">Token counts from the API response.</param>
    /// <param name="isBatch">True for Message Batches API calls (50% discount).</param>
    /// <remarks>
    /// Batch + cache composition: batch-cached reads are priced at
    /// <c>CacheReadPerMtok × 0.5</c> per Anthropic's published policy
    /// (input × 0.10 × 0.5 = 0.05×). Verify this at each pricing update.
    /// </remarks>
    public decimal CalculateUsd(ModelId model, TokenUsage usage, bool isBatch)
    {
        var rates = CostRates.ForModel(model);

        if (!isBatch)
        {
            return rates.ComputeUsd(
                inputTokens:       usage.InputTokens,
                outputTokens:      usage.OutputTokens,
                cacheWriteTokens:  usage.CacheCreationInputTokens,
                cacheReadTokens:   usage.CacheReadInputTokens);
        }

        // Batch: batch input/output rates; cache rates halved (compose with batch).
        return (long)usage.InputTokens              / 1_000_000m * rates.BatchInputPerMtok
             + (long)usage.OutputTokens             / 1_000_000m * rates.BatchOutputPerMtok
             + (long)usage.CacheCreationInputTokens / 1_000_000m * rates.CacheWritePerMtok * 0.5m
             + (long)usage.CacheReadInputTokens     / 1_000_000m * rates.CacheReadPerMtok  * 0.5m;
    }
}
