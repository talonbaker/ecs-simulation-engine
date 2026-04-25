namespace Warden.Anthropic;

/// <summary>
/// Abstraction over the Anthropic Messages and Batches APIs.
/// Implemented by <see cref="AnthropicClient"/> (real) and
/// <c>MockAnthropic</c> (test/offline) in Warden.Orchestrator.
/// </summary>
public interface IAnthropicClient
{
    Task<MessageResponse> CreateMessageAsync(
        MessageRequest request,
        CancellationToken ct = default);

    Task<BatchSubmission> CreateBatchAsync(
        BatchRequest request,
        CancellationToken ct = default);

    Task<BatchStatus> GetBatchAsync(
        string batchId,
        CancellationToken ct = default);

    IAsyncEnumerable<BatchResultEntry> StreamBatchResultsAsync(
        string batchId,
        CancellationToken ct = default);
}
