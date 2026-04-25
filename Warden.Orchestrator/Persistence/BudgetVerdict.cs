namespace Warden.Orchestrator.Persistence;

/// <summary>
/// Returned by <see cref="BudgetGuard.Check"/> before each dispatch.
/// When <see cref="CanProceed"/> is false the caller must halt; the run is over.
/// </summary>
public sealed record BudgetVerdict(
    bool     CanProceed,
    decimal  SpentUsd,
    decimal  RemainingUsd,
    string?  HaltReason);
