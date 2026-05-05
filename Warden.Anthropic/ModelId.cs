using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warden.Anthropic;

/// <summary>
/// Strongly-typed model identifier. Construction is restricted to the static
/// factory members so callers outside this assembly cannot introduce arbitrary
/// model strings. The <see cref="ModelIdJsonConverter"/> handles JSON round-trips.
/// </summary>
[JsonConverter(typeof(ModelIdJsonConverter))]
public readonly record struct ModelId
{
    /// <summary>The raw model name sent on the wire, e.g. "claude-sonnet-4-6".</summary>
    public string Name { get; }

    // Private: external assemblies may not call new ModelId("anything").
    private ModelId(string name) => Name = name;

    /// <summary>claude-opus-4-6</summary>
    public static readonly ModelId OpusV46   = new("claude-opus-4-6");
    /// <summary>claude-sonnet-4-6</summary>
    public static readonly ModelId SonnetV46 = new("claude-sonnet-4-6");
    /// <summary>claude-haiku-4-5-20251001</summary>
    public static readonly ModelId HaikuV45  = new("claude-haiku-4-5-20251001");

    /// <inheritdoc/>
    public override string ToString() => Name ?? string.Empty;

    // -- JSON converter -----------------------------------------------------------
    // Nested private class: CAN access the enclosing type's private constructor.
    // Wire format: a plain JSON string, e.g. "claude-sonnet-4-6".

    private sealed class ModelIdJsonConverter : JsonConverter<ModelId>
    {
        public override ModelId Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var name = reader.GetString()
                ?? throw new JsonException("ModelId cannot be null.");
            // Inner class can call the private constructor.
            return new ModelId(name);
        }

        public override void Write(
            Utf8JsonWriter writer,
            ModelId value,
            JsonSerializerOptions options)
            => writer.WriteStringValue(value.Name);
    }
}
