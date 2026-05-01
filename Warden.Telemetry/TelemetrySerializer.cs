using System.Text.Json;
using Warden.Contracts;
using Warden.Contracts.Telemetry;

namespace Warden.Telemetry;

/// <summary>
/// Serialises <see cref="WorldStateDto"/> to JSON using <see cref="JsonOptions.Wire"/>.
///
/// TWO FORMATS
/// ───────────
/// <see cref="SerializeSnapshot"/> — single compact JSON object.
///   Use for <c>ECSCli ai snapshot --out world.json</c>.
///
/// <see cref="SerializeFrame"/> — one-line JSON with a trailing newline.
///   Append-safe; concatenated lines form a valid JSONL stream.
///   Use for <c>ECSCli ai stream</c>.
///
/// Neither method ever pretty-prints. Pretty-printing is a CLI flag (WP-04),
/// not a library concern.
/// </summary>
public static class TelemetrySerializer
{
    /// <summary>
    /// Serialises <paramref name="dto"/> to a compact JSON string (no trailing newline).
    /// </summary>
    public static string SerializeSnapshot(WorldStateDto dto)
        => JsonSerializer.Serialize(dto, JsonOptions.Wire);

    /// <summary>
    /// Serialises <paramref name="dto"/> to a single-line JSON string terminated
    /// with a Unix newline (<c>\n</c>), suitable for JSONL streaming.
    /// </summary>
    public static string SerializeFrame(WorldStateDto dto)
        => JsonSerializer.Serialize(dto, JsonOptions.Wire) + "\n";

    /// <summary>
    /// Deserialises a JSON string produced by <see cref="SerializeSnapshot"/> or
    /// <see cref="SerializeFrame"/> back to a <see cref="WorldStateDto"/>.
    /// Returns null when <paramref name="json"/> is null or whitespace.
    /// </summary>
    public static WorldStateDto? DeserializeSnapshot(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<WorldStateDto>(json, JsonOptions.Wire);
    }
}