using Warden.Contracts.Handshake;

namespace Warden.Orchestrator.Dispatcher;

/// <summary>
/// The orchestrator's verdict after evaluating a worker result against the fail-closed policy.
/// Produced exclusively by <see cref="FailClosedEscalator"/>; never constructed ad-hoc.
/// </summary>
public sealed record EscalationVerdict(
    bool        ProceedDownstream,
    string      HumanMessage,
    OutcomeCode TerminalOutcome);
