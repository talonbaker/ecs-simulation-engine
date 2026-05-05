using System.Text.Json.Serialization;

namespace Warden.Anthropic;

/// <summary>
/// One line of the JSONL results stream returned by
/// <c>GET /v1/messages/batches/{id}/results</c>.
/// </summary>
/// <param name="CustomId">
/// The <c>custom_id</c> that was supplied in the original <see cref="BatchRequestEntry"/>.
/// </param>
/// <param name="Result">The outcome of the individual request.</param>
public sealed record BatchResultEntry(
    [property: JsonPropertyName("custom_id")] string          CustomId,
    BatchEntryResult                                           Result);

// -- Polymorphic result union ---------------------------------------------------

/// <summary>
/// Discriminated union over the four possible per-entry outcomes.
/// The <c>"type"</c> JSON property is the discriminator.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SucceededResult), "succeeded")]
[JsonDerivedType(typeof(ErroredResult),   "errored")]
[JsonDerivedType(typeof(CanceledResult),  "canceled")]
[JsonDerivedType(typeof(ExpiredResult),   "expired")]
public abstract record BatchEntryResult;

/// <summary>The request completed successfully.</summary>
/// <param name="Message">The full <see cref="MessageResponse"/> for this entry.</param>
public sealed record SucceededResult(MessageResponse Message) : BatchEntryResult;

/// <summary>The request failed with an API error.</summary>
/// <param name="Error">Error details.</param>
public sealed record ErroredResult(BatchEntryError Error) : BatchEntryResult;

/// <summary>The request was canceled before it was processed.</summary>
public sealed record CanceledResult : BatchEntryResult;

/// <summary>The request expired (batch was not processed within 24 hours).</summary>
public sealed record ExpiredResult : BatchEntryResult;

/// <summary>Error payload within an <see cref="ErroredResult"/>.</summary>
/// <param name="Type">Error type identifier, e.g. <c>"server_error"</c>.</param>
/// <param name="Message">Human-readable error description.</param>
public sealed record BatchEntryError(string Type, string Message);
