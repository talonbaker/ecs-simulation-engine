using System.Text.Json.Serialization;

namespace Warden.Anthropic;

/// <summary>
/// Request body for <c>POST /v1/messages</c>.
/// All fields map to the Anthropic API's documented shape; snake_case names
/// are applied via <see cref="JsonPropertyNameAttribute"/>.
/// </summary>
/// <param name="Model">The model to invoke.</param>
/// <param name="MaxTokens">Maximum tokens to generate in the response.</param>
/// <param name="Messages">Conversation history. Must alternate user/assistant turns.</param>
public sealed record MessageRequest(
    ModelId              Model,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    List<MessageTurn>    Messages)
{
    /// <summary>
    /// Optional system prompt blocks. Attach <see cref="CacheControl"/> markers here
    /// to cache static context (engine docs, schemas, etc.).
    /// </summary>
    public List<ContentBlock>? System { get; init; }

    /// <summary>Sampling temperature in [0, 1]. Null uses the model default.</summary>
    public double? Temperature { get; init; }

    /// <summary>Optional per-request metadata passed to Anthropic.</summary>
    public MessageMetadata? Metadata { get; init; }
}

/// <summary>
/// Per-request metadata forwarded to the Anthropic API.
/// Useful for associating requests with end-user IDs in the Anthropic dashboard.
/// </summary>
/// <param name="UserId">
/// An opaque user identifier. Should be a hash or UUID — never a PII string.
/// </param>
public sealed record MessageMetadata(
    [property: JsonPropertyName("user_id")] string? UserId = null);
