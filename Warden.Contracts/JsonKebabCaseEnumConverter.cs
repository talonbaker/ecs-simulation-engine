using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warden.Contracts;

/// <summary>
/// Generic <see cref="JsonConverter{T}"/> that serialises any enum as a
/// lowercase kebab-case string and deserialises it back.
///
/// <code>
/// AmbiguousSpec  ↔  "ambiguous-spec"
/// BuildFailed    ↔  "build-failed"
/// CliNonzero     ↔  "cli-nonzero"
/// </code>
///
/// Attach to an enum type via:
/// <code>[JsonConverter(typeof(JsonKebabCaseEnumConverter&lt;MyEnum&gt;))]</code>
/// </summary>
public sealed class JsonKebabCaseEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected string token for {typeof(T).Name}, got {reader.TokenType}.");

        var kebab = reader.GetString()!;

        // "ambiguous-spec" → split on '-' → ["ambiguous","spec"] → PascalCase → "AmbiguousSpec"
        var parts = kebab.Split('-');
        var pascal = string.Concat(
            System.Linq.Enumerable.Select(parts,
                p => p.Length == 0 ? string.Empty
                   : char.ToUpperInvariant(p[0]) + p.Substring(1)));

        if (Enum.TryParse<T>(pascal, ignoreCase: false, out var result))
            return result;

        // Fallback: case-insensitive search
        if (Enum.TryParse<T>(pascal, ignoreCase: true, out result))
            return result;

        throw new JsonException($"'{kebab}' is not a valid value for {typeof(T).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var name  = value.ToString()!;
        var kebab = JsonNamingPolicy.KebabCaseLower.ConvertName(name);
        writer.WriteStringValue(kebab);
    }
}
