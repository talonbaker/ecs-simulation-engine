namespace Warden.Orchestrator.Dispatcher;

/// <summary>Progress report emitted by <see cref="ConcurrencyController"/> after each slot change.</summary>
public sealed record SonnetProgress(
    string WorkerId,
    string SpecId,
    string Status);
