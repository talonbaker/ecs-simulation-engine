using Warden.Contracts.Handshake;

namespace Warden.Orchestrator.Batch;

/// <summary>
/// Orchestrator-internal record tracking an in-flight batch while polling.
/// Distinct from <see cref="Warden.Anthropic.BatchSubmission"/>, which is the raw API response.
/// </summary>
internal sealed record BatchSubmission(
    string                     RunId,
    string                     BatchId,
    List<ScenarioDto>          UniqueScenarios,
    Dictionary<string, string> DupToOrig);
