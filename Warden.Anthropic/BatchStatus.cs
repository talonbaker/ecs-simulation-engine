using System.Text.Json.Serialization;

namespace Warden.Anthropic;

/// <summary>
/// Poll result from <c>GET /v1/messages/batches/{id}</c>.
/// When <see cref="ProcessingStatus"/> is <see cref="BatchProcessingStatus.Ended"/>,
/// use <see cref="AnthropicClient.StreamBatchResultsAsync"/> to retrieve results.
/// </summary>
/// <param name="Id">Batch identifier.</param>
/// <param name="ProcessingStatus">Current lifecycle phase.</param>
/// <param name="RequestCounts">Per-status entry counts.</param>
/// <param name="EndedAt">UTC timestamp when all processing finished; null while running.</param>
/// <param name="CreatedAt">UTC timestamp when the batch was submitted.</param>
/// <param name="ExpiresAt">UTC timestamp after which the batch and results are discarded.</param>
/// <param name="ResultsUrl">URL to stream JSONL results; null while processing.</param>
public sealed record BatchStatus(
    string                                                                  Id,
    [property: JsonPropertyName("processing_status")] BatchProcessingStatus ProcessingStatus,
    [property: JsonPropertyName("request_counts")]    BatchRequestCounts    RequestCounts,
    [property: JsonPropertyName("ended_at")]          DateTimeOffset?       EndedAt,
    [property: JsonPropertyName("created_at")]        DateTimeOffset        CreatedAt,
    [property: JsonPropertyName("expires_at")]        DateTimeOffset        ExpiresAt,
    [property: JsonPropertyName("results_url")]       string?               ResultsUrl);

/// <summary>Per-status request counts within a batch.</summary>
/// <param name="Processing">Requests still being processed.</param>
/// <param name="Succeeded">Requests that completed successfully.</param>
/// <param name="Errored">Requests that returned an API error.</param>
/// <param name="Canceled">Requests canceled before processing.</param>
/// <param name="Expired">Requests that expired before processing.</param>
public sealed record BatchRequestCounts(
    int Processing,
    int Succeeded,
    int Errored,
    int Canceled,
    int Expired);

/// <summary>Lifecycle phase of a message batch.</summary>
[JsonConverter(typeof(JsonSnakeCaseEnumConverter<BatchProcessingStatus>))]
public enum BatchProcessingStatus
{
    /// <summary>The batch is being processed by Anthropic.</summary>
    InProgress,
    /// <summary>A cancellation request has been submitted; not all requests have been canceled yet.</summary>
    Canceling,
    /// <summary>All requests have a terminal result (succeeded, errored, canceled, or expired).</summary>
    Ended,
}
