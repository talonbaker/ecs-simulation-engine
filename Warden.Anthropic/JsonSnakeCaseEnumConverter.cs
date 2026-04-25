using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warden.Anthropic;

/// <summary>
/// Generic <see cref="JsonConverter{T}"/> that serialises any enum as a
/// lowercase snake_case string and deserialises it back.
///
/// <code>
/// InProgress  ↔  "in_progress"
/// Ended       ↔  "ended"
/// </code>
///
/// Attach to an enum type via:
/// <code>[JsonConverter(typeof(JsonSnakeCaseEnumConverter&lt;MyEnum&gt;))]</code>
///
/// <para>Uses <see cref="JsonNamingPolicy.SnakeCaseLower"/> (.NET 8+).</para>
/// </summary>
public sealed class JsonSnakeCaseEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    public override T Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException(
                $"Expected string token for {typeof(T).Name}, got {reader.TokenType}.");

        var snake = reader.GetString()!;

        // "in_progress" → split on '_' → ["in","progress"] → PascalCase → "InProgress"
        var parts = snake.Split('_');
        var pascal = string.Concat(
            System.Linq.Enumerable.Select(parts,
                p => p.Length == 0 ? string.Empty
                   : char.ToUpperInvariant(p[0]) + p.Substring(1)));

        if (Enum.TryParse<T>(pascal, ignoreCase: false, out var result))
            return result;

        // Fallback: case-insensitive
        if (Enum.TryParse<T>(pascal, ignoreCase: true, out result))
            return result;

        throw new JsonException($"'{snake}' is not a valid value for {typeof(T).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var snake = JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()!);
        writer.WriteStringValue(snake);
    }
}
