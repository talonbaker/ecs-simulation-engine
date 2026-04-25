using System.Text.Json.Serialization;

namespace Warden.Anthropic;

/// <summary>
/// Response from <c>POST /v1/messages/batches</c>: the newly-created batch.
/// Poll <see cref="AnthropicClient.GetBatchAsync"/> until
/// <see cref="ProcessingStatus"/> is <c>"ended"</c>.
/// </summary>
/// <param name="Id">Batch identifier, e.g. <c>"msgbatch_013Zva2CMHLNnXjNJKqJ2EwZ"</c>.</param>
/// <param name="Type">Always <c>"message_batch"</c>.</param>
/// <param name="ProcessingStatus">Current processing phase: <c>"in_progress"</c> | <c>"canceling"</c> | <c>"ended"</c>.</param>
/// <param name="CreatedAt">UTC timestamp when the batch was submitted.</param>
/// <param name="ResultsUrl">
/// URL to stream JSONL results once <see cref="ProcessingStatus"/> is <c>"ended"</c>.
/// Null while still processing.
/// </param>
public sealed record BatchSubmission(
    string                                                       Id,
    string                                                       Type,
    [property: JsonPropertyName("processing_status")] string    ProcessingStatus,
    [property: JsonPropertyName("created_at")]  DateTimeOffset  CreatedAt,
    [property: JsonPropertyName("results_url")] string?         ResultsUrl);
