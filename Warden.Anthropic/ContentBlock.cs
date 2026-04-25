using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warden.Anthropic;

/// <summary>
/// Tagged union over the three content block types the orchestrator uses.
/// The <c>"type"</c> JSON property is the discriminator.
///
/// <para>
/// <see cref="CacheControl"/> is an optional field on every concrete block.
/// The orchestrator's cache manager (WP-06) decides which blocks to mark;
/// this client preserves those markers verbatim on the wire.
/// </para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextBlock),       "text")]
[JsonDerivedType(typeof(ToolUseBlock),    "tool_use")]
[JsonDerivedType(typeof(ToolResultBlock), "tool_result")]
public abstract record ContentBlock;

// ── Concrete block types ────────────────────────────────────────────────────────

/// <summary>A plain text content block.</summary>
/// <param name="Text">The text content.</param>
/// <param name="CacheControl">Optional prompt-cache marker.</param>
public sealed record TextBlock(
    string       Text,
    [property: JsonPropertyName("cache_control")] CacheControl? CacheControl = null
) : ContentBlock;

/// <summary>
/// A tool-use request from the model. <see cref="Input"/> is an arbitrary JSON
/// object whose shape is defined by the tool schema.
/// </summary>
/// <param name="Id">Tool use ID, e.g. <c>"toolu_01A09q90qw90lq917835lq9"</c>.</param>
/// <param name="Name">Tool name.</param>
/// <param name="Input">Arbitrary JSON input object.</param>
/// <param name="CacheControl">Optional prompt-cache marker.</param>
public sealed record ToolUseBlock(
    string       Id,
    string       Name,
    JsonElement  Input,
    [property: JsonPropertyName("cache_control")] CacheControl? CacheControl = null
) : ContentBlock;

/// <summary>
/// A tool result returned by the caller to the model.
/// </summary>
/// <param name="ToolUseId">The ID of the preceding tool-use block this result answers.</param>
/// <param name="Content">Result content blocks (text or nested blocks).</param>
/// <param name="IsError">When true, the tool invocation failed.</param>
/// <param name="CacheControl">Optional prompt-cache marker.</param>
public sealed record ToolResultBlock(
    [property: JsonPropertyName("tool_use_id")] string         ToolUseId,
    List<ContentBlock>?                                         Content    = null,
    [property: JsonPropertyName("is_error")]    bool?          IsError    = null,
    [property: JsonPropertyName("cache_control")] CacheControl? CacheControl = null
) : ContentBlock;
