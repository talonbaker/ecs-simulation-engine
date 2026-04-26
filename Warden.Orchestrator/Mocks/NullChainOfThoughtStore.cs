using Warden.Orchestrator.Dispatcher;

namespace Warden.Orchestrator.Mocks;

/// <summary>
/// No-op <see cref="IChainOfThoughtStore"/> used in mock and dry-run modes.
/// Prints a one-line notice to stdout so the operator knows persistence was skipped.
/// Real disk persistence is implemented in WP-10.
/// </summary>
public sealed class NullChainOfThoughtStore : IChainOfThoughtStore
{
    public Task PersistSpecAsync(string runId, string sonnetId, string specJson, CancellationToken ct = default)
    {
        Console.WriteLine($"[NullCoT] {sonnetId}/spec.json — skipped (WP-10)");
        return Task.CompletedTask;
    }

    public Task PersistPromptAsync(string runId, string sonnetId, string promptText, CancellationToken ct = default)
    {
        Console.WriteLine($"[NullCoT] {sonnetId}/prompt.txt — skipped (WP-10)");
        return Task.CompletedTask;
    }

    public Task PersistRawResponseAsync(string runId, string sonnetId, string responseJson, CancellationToken ct = default)
    {
        Console.WriteLine($"[NullCoT] {sonnetId}/response.json — skipped (WP-10)");
        return Task.CompletedTask;
    }

    public Task PersistResultAsync(string runId, string sonnetId, string resultJson, CancellationToken ct = default)
    {
        Console.WriteLine($"[NullCoT] {sonnetId}/result.json — skipped (WP-10)");
        return Task.CompletedTask;
    }

    public Task AppendEventAsync(string runId, string eventJson, CancellationToken ct = default)
    {
        Console.WriteLine($"[NullCoT] events.jsonl ← {eventJson} — skipped");
        return Task.CompletedTask;
    }
}
