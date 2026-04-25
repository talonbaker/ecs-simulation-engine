using System.Text.Json.Serialization;

namespace Warden.Anthropic;

/// <summary>
/// Request body for <c>POST /v1/messages/batches</c>.
/// Each entry is a <see cref="MessageRequest"/> tagged with a caller-assigned
/// <c>custom_id</c> that echoes back in the result JSONL.
/// </summary>
/// <param name="Requests">Up to 100,000 tagged message requests per batch.</param>
public sealed record BatchRequest(List<BatchRequestEntry> Requests);

/// <summary>
/// One entry in a <see cref="BatchRequest"/>.
/// </summary>
/// <param name="CustomId">
/// Caller-assigned string identifier (max 64 chars). Must be unique within the batch.
/// Echoed verbatim in <see cref="BatchResultEntry.CustomId"/>.
/// </param>
/// <param name="Params">The message request parameters for this entry.</param>
public sealed record BatchRequestEntry(
    [property: JsonPropertyName("custom_id")] string         CustomId,
    MessageRequest                                            Params);
