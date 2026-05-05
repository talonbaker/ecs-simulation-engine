namespace Warden.Anthropic;

/// <summary>
/// Source of truth for per-model token cost coefficients (USD per million tokens).
/// Figures match the table in <c>docs/c2-infrastructure/02-cost-model.md §1</c>.
///
/// <para>
/// <b>Update protocol:</b> when Anthropic's pricing page changes, update
/// <see cref="PricedAsOf"/> and the rate constants here, then run
/// <c>dotnet test --filter CostRates</c>. AT-08 will fail if the date is stale.
/// </para>
/// </summary>
public static class CostRates
{
    /// <summary>
    /// The date on which these rates were last verified against Anthropic's
    /// published pricing. AT-08 asserts this is ≥ 2026-04-01.
    /// </summary>
    public static readonly DateOnly PricedAsOf = new(2026, 4, 24);

    // -- Per-model rate tables (USD / million tokens) -----------------------------

    /// <summary>Rates for <see cref="ModelId.OpusV46"/>.</summary>
    public static readonly ModelRates Opus = new(
        InputPerMtok:       15.00m,
        OutputPerMtok:      75.00m,
        CacheWritePerMtok:  18.75m,
        CacheReadPerMtok:    1.50m,
        BatchInputPerMtok:   7.50m,
        BatchOutputPerMtok: 37.50m);

    /// <summary>Rates for <see cref="ModelId.SonnetV46"/>.</summary>
    public static readonly ModelRates Sonnet = new(
        InputPerMtok:       3.00m,
        OutputPerMtok:     15.00m,
        CacheWritePerMtok:  3.75m,
        CacheReadPerMtok:   0.30m,
        BatchInputPerMtok:  1.50m,
        BatchOutputPerMtok: 7.50m);

    /// <summary>Rates for <see cref="ModelId.HaikuV45"/>.</summary>
    public static readonly ModelRates Haiku = new(
        InputPerMtok:       1.00m,
        OutputPerMtok:      5.00m,
        CacheWritePerMtok:  1.25m,
        CacheReadPerMtok:   0.10m,
        BatchInputPerMtok:  0.50m,
        BatchOutputPerMtok: 2.50m);

    /// <summary>Returns the rate table for the given model.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Unknown model ID.</exception>
    public static ModelRates ForModel(ModelId model)
    {
        if (model == ModelId.OpusV46)   return Opus;
        if (model == ModelId.SonnetV46) return Sonnet;
        if (model == ModelId.HaikuV45)  return Haiku;
        throw new ArgumentOutOfRangeException(nameof(model),
            $"No cost rates registered for model '{model.Name}'.");
    }
}

/// <summary>
/// Per-million-token cost coefficients for one model.
/// All values are in USD per million tokens.
/// </summary>
public sealed record ModelRates(
    decimal InputPerMtok,
    decimal OutputPerMtok,
    decimal CacheWritePerMtok,
    decimal CacheReadPerMtok,
    decimal BatchInputPerMtok,
    decimal BatchOutputPerMtok)
{
    /// <summary>
    /// Computes the USD cost for an interactive (non-batch) call given token counts.
    /// </summary>
    public decimal ComputeUsd(
        long inputTokens,
        long outputTokens,
        long cacheWriteTokens = 0L,
        long cacheReadTokens  = 0L)
        => inputTokens      / 1_000_000m * InputPerMtok
         + outputTokens     / 1_000_000m * OutputPerMtok
         + cacheWriteTokens / 1_000_000m * CacheWritePerMtok
         + cacheReadTokens  / 1_000_000m * CacheReadPerMtok;

    /// <summary>
    /// Computes the USD cost for a batch call given token counts.
    /// </summary>
    public decimal ComputeBatchUsd(long inputTokens, long outputTokens)
        => inputTokens  / 1_000_000m * BatchInputPerMtok
         + outputTokens / 1_000_000m * BatchOutputPerMtok;
}
