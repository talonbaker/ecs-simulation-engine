namespace Warden.Anthropic;

/// <summary>
/// Prompt-cache control marker attached to a <see cref="ContentBlock"/>.
///
/// <para>
/// Allowed TTL values (when using the extended-cache beta): <c>"5m"</c> or
/// <c>"1h"</c>. Omit <see cref="Ttl"/> for the default 5-minute ephemeral window.
/// </para>
///
/// Wire format examples:
/// <code>
/// {"type":"ephemeral"}
/// {"type":"ephemeral","ttl":"1h"}
/// </code>
/// </summary>
/// <param name="Type">Cache control type. Must be <c>"ephemeral"</c>.</param>
/// <param name="Ttl">Optional TTL. One of <c>"5m"</c> or <c>"1h"</c>; null omits the field.</param>
public sealed record CacheControl(string Type = "ephemeral", string? Ttl = null);
