using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Warden.Contracts;
using Warden.Contracts.Handshake;
using Warden.Contracts.SchemaValidation;
using Warden.Contracts.Telemetry;
using Xunit;

namespace Warden.Contracts.Tests;

/// <summary>
/// AT-01 — Each C# record type round-trips through <see cref="JsonOptions.Wire"/>
///          to semantically equivalent JSON that still satisfies its schema.
///
/// AT-06 — <see cref="JsonOptions.Wire"/> and <see cref="JsonOptions.Pretty"/>
///          produce bit-for-bit identical content when the Pretty form is
///          re-normalised through Wire.
/// </summary>
public class SchemaRoundTripTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string LoadSample(string filename)
    {
        var asm          = Assembly.GetExecutingAssembly();
        var resourceName = $"Warden.Contracts.Tests.Samples.{filename}";
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded sample resource '{resourceName}' not found. " +
                $"Available: {string.Join(", ", asm.GetManifestResourceNames())}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 1. Validates the sample JSON against its schema.
    /// 2. Deserialises to <typeparamref name="T"/> via <see cref="JsonOptions.Wire"/>.
    /// 3. Re-serialises; validates result against schema.
    /// 4. Re-serialises a second time; asserts idempotent (AT-01).
    /// </summary>
    private static void AssertRoundTrip<T>(string filename, Schema schema)
    {
        var sampleJson = LoadSample(filename);

        // Step 1 — sample must be schema-valid
        var v1 = SchemaValidator.Validate(sampleJson, schema);
        Assert.True(v1.IsValid,
            $"Sample '{filename}' failed schema validation: {string.Join("; ", v1.Errors)}");

        // Step 2-3 — round-trip through Wire
        var obj   = JsonSerializer.Deserialize<T>(sampleJson, JsonOptions.Wire)!;
        var json2 = JsonSerializer.Serialize(obj, JsonOptions.Wire);

        var v2 = SchemaValidator.Validate(json2, schema);
        Assert.True(v2.IsValid,
            $"Re-serialised '{filename}' failed schema validation: {string.Join("; ", v2.Errors)}");

        // Step 4 — serialisation is idempotent
        var obj2  = JsonSerializer.Deserialize<T>(json2, JsonOptions.Wire)!;
        var json3 = JsonSerializer.Serialize(obj2, JsonOptions.Wire);
        Assert.Equal(json2, json3);
    }

    // ── AT-01: round-trip tests ────────────────────────────────────────────────

    [Fact]
    public void WorldState_RoundTrips() =>
        AssertRoundTrip<WorldStateDto>("world-state.sample.json", Schema.WorldState);

    [Fact]
    public void OpusToSonnet_RoundTrips() =>
        AssertRoundTrip<OpusSpecPacket>("opus-to-sonnet.sample.json", Schema.OpusToSonnet);

    [Fact]
    public void SonnetResult_RoundTrips() =>
        AssertRoundTrip<SonnetResult>("sonnet-result.sample.json", Schema.SonnetResult);

    [Fact]
    public void SonnetToHaiku_RoundTrips() =>
        AssertRoundTrip<ScenarioBatch>("sonnet-to-haiku.sample.json", Schema.SonnetToHaiku);

    [Fact]
    public void HaikuResult_RoundTrips() =>
        AssertRoundTrip<HaikuResult>("haiku-result.sample.json", Schema.HaikuResult);

    [Fact]
    public void AiCommandBatch_RoundTrips() =>
        AssertRoundTrip<AiCommandBatch>("ai-command-batch.sample.json", Schema.AiCommandBatch);

    // ── AT-06: Wire vs Pretty content identity ─────────────────────────────────

    [Fact]
    public void JsonOptions_Wire_And_Pretty_Produce_Same_Content()
    {
        var sampleJson = LoadSample("haiku-result.sample.json");
        var obj        = JsonSerializer.Deserialize<HaikuResult>(sampleJson, JsonOptions.Wire)!;

        var wire   = JsonSerializer.Serialize(obj, JsonOptions.Wire);
        var pretty = JsonSerializer.Serialize(obj, JsonOptions.Pretty);

        // Normalise pretty through Wire to strip indentation, then compare
        var objFromPretty  = JsonSerializer.Deserialize<HaikuResult>(pretty, JsonOptions.Wire)!;
        var wireNormalized = JsonSerializer.Serialize(objFromPretty, JsonOptions.Wire);

        Assert.Equal(wire, wireNormalized);
    }

    [Fact]
    public void JsonOptions_Pretty_Is_A_Superset_Of_Wire_Content_For_All_Types()
    {
        // Verify multiple types, not just one
        AssertWireEqualsNormalisedPretty<WorldStateDto>(
            LoadSample("world-state.sample.json"));
        AssertWireEqualsNormalisedPretty<SonnetResult>(
            LoadSample("sonnet-result.sample.json"));
        AssertWireEqualsNormalisedPretty<ScenarioBatch>(
            LoadSample("sonnet-to-haiku.sample.json"));
    }

    private static void AssertWireEqualsNormalisedPretty<T>(string sampleJson)
    {
        var obj    = JsonSerializer.Deserialize<T>(sampleJson, JsonOptions.Wire)!;
        var wire   = JsonSerializer.Serialize(obj, JsonOptions.Wire);
        var pretty = JsonSerializer.Serialize(obj, JsonOptions.Pretty);

        var obj2   = JsonSerializer.Deserialize<T>(pretty, JsonOptions.Wire)!;
        var wire2  = JsonSerializer.Serialize(obj2, JsonOptions.Wire);

        Assert.Equal(wire, wire2);
    }
}
