using Warden.Contracts.Handshake;

namespace Warden.Orchestrator.Dispatcher;

/// <summary>
/// Persists Sonnet-tier chain-of-thought artefacts under
/// <c>./runs/&lt;runId&gt;/sonnet-&lt;n&gt;/</c>.
/// Implementation lands in WP-10; this packet declares the interface
/// and ships a no-op stub (<see cref="Mocks.NullChainOfThoughtStore"/>).
/// </summary>
public interface IChainOfThoughtStore
{
    Task PersistSpecAsync(string runId, string sonnetId, string specJson, CancellationToken ct = default);
    Task PersistPromptAsync(string runId, string sonnetId, string promptText, CancellationToken ct = default);
    Task PersistRawResponseAsync(string runId, string sonnetId, string responseJson, CancellationToken ct = default);
    Task PersistResultAsync(string runId, string sonnetId, string resultJson, CancellationToken ct = default);
    Task AppendEventAsync(string runId, string eventJson, CancellationToken ct = default);
}
