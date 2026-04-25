namespace Warden.Orchestrator.Persistence;

/// <summary>
/// Tracks running spend against a hard per-run USD ceiling.
/// Thread-safe; concurrent Sonnet and batch-scheduler callers are expected.
/// </summary>
public sealed class BudgetGuard
{
    private readonly decimal _budgetUsd;
    private decimal _spentUsd;
    private readonly object _lock = new();

    public BudgetGuard(decimal budgetUsd) => _budgetUsd = budgetUsd;

    /// <summary>Current accumulated spend (snapshot; may advance immediately after read).</summary>
    public decimal SpentUsd { get { lock (_lock) return _spentUsd; } }

    /// <summary>
    /// Checks whether dispatching a call whose projected cost is
    /// <paramref name="projectedCostUsd"/> would exceed the budget.
    /// Does NOT modify the running sum; call <see cref="Record"/> after the
    /// actual API response is received.
    /// </summary>
    public BudgetVerdict Check(decimal projectedCostUsd)
    {
        lock (_lock)
        {
            var remaining = _budgetUsd - _spentUsd;
            if (projectedCostUsd > remaining)
            {
                return new BudgetVerdict(
                    CanProceed:   false,
                    SpentUsd:     _spentUsd,
                    RemainingUsd: remaining,
                    HaltReason:   "budget-exceeded");
            }
            return new BudgetVerdict(
                CanProceed:   true,
                SpentUsd:     _spentUsd,
                RemainingUsd: remaining,
                HaltReason:   null);
        }
    }

    /// <summary>
    /// Adds <paramref name="actualCostUsd"/> to the running sum after a call
    /// completes. Called once per API response, after <see cref="Check"/> approved
    /// the dispatch.
    /// </summary>
    public void Record(decimal actualCostUsd)
    {
        lock (_lock)
        {
            _spentUsd += actualCostUsd;
        }
    }
}
