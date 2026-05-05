using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warden.Contracts;

/// <summary>
/// The canonical <see cref="JsonSerializerOptions"/> used by every tier when
/// serialising or deserialising Warden cross-tier messages.
///
/// RULES
/// -----
/// - Property names are camelCase in JSON (C# PascalCase ↔ JSON camelCase).
/// - Enums are serialised as strings. Each enum type applies its own naming
///   policy via a [JsonConverter] attribute (see OutcomeCode, BlockReason, etc.).
/// - Null properties are omitted from the output (WhenWritingNull).
/// - No indentation — wire format is compact. Use JsonWriterOptions for pretty-
///   printing in diagnostics.
/// - Fields are NOT included (records and DTOs use properties only).
///
/// USAGE
/// -----
///   string json = JsonSerializer.Serialize(result, JsonOptions.Wire);
///   SonnetResult? r = JsonSerializer.Deserialize&lt;SonnetResult&gt;(json, JsonOptions.Wire);
/// </summary>
public static class JsonOptions
{
    /// <summary>
    /// Wire-format options: camelCase, no indent, nulls omitted.
    /// Use for all API messages and persisted files.
    /// </summary>
    public static readonly JsonSerializerOptions Wire = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented               = false,
        IncludeFields               = false,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            // Routes every enum to the right policy:
            //   - enums with [JsonConverter] on the type → that converter
            //   - everything else → camelCase string
            // See JsonSmartEnumConverterFactory for the full explanation.
            new JsonSmartEnumConverterFactory()
        }
    };

    /// <summary>
    /// Human-readable options: same as <see cref="Wire"/> but indented.
    /// Use for diagnostics, logs, and the completion notes — never for API calls.
    /// </summary>
    public static readonly JsonSerializerOptions Pretty = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented               = true,
        IncludeFields               = false,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonSmartEnumConverterFactory()
        }
    };
}
