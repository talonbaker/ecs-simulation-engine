using System;
using System.Collections.Generic;
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

    // ── v0.2 schema round-trip tests ──────────────────────────────────────────

    /// <summary>
    /// AT-02 — The pre-existing v0.1 sample round-trips under the v0.2 schema.
    /// Additive compatibility: v0.2 schema must accept v0.1 messages by treating
    /// new optional fields as absent.
    /// </summary>
    [Fact]
    public void WorldState_V01SampleRoundTripsUnderV02Schema() =>
        AssertRoundTrip<WorldStateDto>("world-state.sample.json", Schema.WorldState);

    /// <summary>
    /// AT-03 — The new v0.2 canonical sample round-trips clean.
    /// </summary>
    [Fact]
    public void WorldState_V02SampleRoundTrips() =>
        AssertRoundTrip<WorldStateDto>("world-state-v02.json", Schema.WorldState);

    /// <summary>
    /// AT-04 — A relationship with three patterns fails maxItems: 2.
    /// </summary>
    [Fact]
    public void WorldState_V02_RelationshipThreePatterns_FailsMaxItems()
    {
        const string json = """
            {
              "schemaVersion": "0.2.0",
              "capturedAt": "2026-04-24T10:00:00+00:00",
              "tick": 1,
              "clock": {
                "gameTimeDisplay": "t", "dayNumber": 1, "isDaytime": true,
                "circadianFactor": 0.5, "timeScale": 1.0
              },
              "entities": [],
              "worldItems": [],
              "worldObjects": [],
              "invariants": { "violationCount": 0 },
              "relationships": [{
                "id": "aaaa0000-0000-0000-0000-000000000000",
                "participantA": "11111111-1111-1111-1111-111111111111",
                "participantB": "22222222-2222-2222-2222-222222222222",
                "patterns": ["rival", "oldFlame", "friend"],
                "pairDrives": { "attraction": 10, "trust": 10, "suspicion": 10, "jealousy": 10 },
                "intensity": 50,
                "historyEventIds": []
              }]
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("array has 3") && e.Contains("maximum is 2"));
    }

    /// <summary>
    /// AT-05 — A memory event description of 281 characters fails maxLength: 280.
    /// </summary>
    [Fact]
    public void WorldState_V02_MemoryEventDescriptionTooLong_FailsMaxLength()
    {
        var longDescription = new string('x', 281);
        var json = $$"""
            {
              "schemaVersion": "0.2.0",
              "capturedAt": "2026-04-24T10:00:00+00:00",
              "tick": 1,
              "clock": {
                "gameTimeDisplay": "t", "dayNumber": 1, "isDaytime": true,
                "circadianFactor": 0.5, "timeScale": 1.0
              },
              "entities": [{
                "id": "11111111-1111-1111-1111-111111111111",
                "shortId": "E1", "name": "A", "species": "human",
                "position": { "x": 0, "y": 0, "z": 0, "hasPosition": true },
                "drives": { "dominant": "None", "eatUrgency": 0, "drinkUrgency": 0,
                            "sleepUrgency": 0, "defecateUrgency": 0, "peeUrgency": 0 },
                "physiology": { "satiation": 50, "hydration": 50, "bodyTemp": 36.5,
                                "energy": 50, "sleepiness": 50, "isSleeping": false }
              }],
              "worldItems": [],
              "worldObjects": [],
              "invariants": { "violationCount": 0 },
              "memoryEvents": [{
                "id": "bbbb0000-0000-0000-0000-000000000000",
                "tick": 1,
                "participants": ["11111111-1111-1111-1111-111111111111"],
                "kind": "test",
                "scope": "pair",
                "description": "{{longDescription}}",
                "persistent": false
              }]
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("281") && e.Contains("maxLength 280"));
    }

    /// <summary>
    /// AT-06 — A memory event with scope "global" is rejected by the referential
    /// checker with the specific reason "global-scope-reserved-for-v0.3".
    /// </summary>
    [Fact]
    public void WorldState_V02_GlobalMemoryScope_RejectedByReferentialChecker()
    {
        var entity = MakeMinimalEntity("11111111-1111-1111-1111-111111111111", "E1", "Alice");
        var dto = MakeMinimalWorldState(new List<EntityStateDto> { entity },
            memoryEvents: new List<MemoryEventDto>
            {
                new()
                {
                    Id           = "bbbb0000-0000-0000-0000-000000000000",
                    Tick         = 1,
                    Participants = new List<string> { entity.Id },
                    Kind         = "test",
                    Scope        = MemoryScope.Global,
                    Description  = "a global event",
                    Persistent   = false
                }
            });

        var result = WorldStateReferentialChecker.Check(dto);

        Assert.False(result.IsValid);
        Assert.Contains("global-scope-reserved-for-v0.3", result.Errors);
    }

    /// <summary>
    /// AT-07 — A relationship whose participantA is not in entities[] is rejected
    /// by the referential checker with a reason naming the missing id.
    /// </summary>
    [Fact]
    public void WorldState_V02_RelationshipParticipantMissing_RejectedByReferentialChecker()
    {
        var entity = MakeMinimalEntity("22222222-2222-2222-2222-222222222222", "E2", "Bob");
        var dto = MakeMinimalWorldState(new List<EntityStateDto> { entity },
            relationships: new List<RelationshipDto>
            {
                new()
                {
                    Id              = "aaaa0000-0000-0000-0000-000000000000",
                    ParticipantA    = "99999999-9999-9999-9999-999999999999", // not in entities
                    ParticipantB    = entity.Id,
                    Patterns        = Array.Empty<RelationshipPattern>(),
                    PairDrives      = new PairDrivesDto(),
                    Intensity       = 0,
                    HistoryEventIds = Array.Empty<string>()
                }
            });

        var result = WorldStateReferentialChecker.Check(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not found in entities"));
    }

    /// <summary>
    /// AT-08 — Two relationships sharing the same unordered pair (A,B) and (B,A)
    /// are rejected with reason "duplicate-pair".
    /// </summary>
    [Fact]
    public void WorldState_V02_DuplicateUnorderedPair_RejectedByReferentialChecker()
    {
        var a = MakeMinimalEntity("11111111-1111-1111-1111-111111111111", "E1", "Alice");
        var b = MakeMinimalEntity("22222222-2222-2222-2222-222222222222", "E2", "Bob");
        var dto = MakeMinimalWorldState(
            new List<EntityStateDto> { a, b },
            relationships: new List<RelationshipDto>
            {
                new()
                {
                    Id = "aaaa0001-0000-0000-0000-000000000000",
                    ParticipantA = a.Id, ParticipantB = b.Id,
                    Patterns = Array.Empty<RelationshipPattern>(),
                    PairDrives = new PairDrivesDto(), Intensity = 0,
                    HistoryEventIds = Array.Empty<string>()
                },
                new()
                {
                    // Same pair, reversed — (B,A) duplicates (A,B)
                    Id = "aaaa0002-0000-0000-0000-000000000000",
                    ParticipantA = b.Id, ParticipantB = a.Id,
                    Patterns = Array.Empty<RelationshipPattern>(),
                    PairDrives = new PairDrivesDto(), Intensity = 0,
                    HistoryEventIds = Array.Empty<string>()
                }
            });

        var result = WorldStateReferentialChecker.Check(dto);

        Assert.False(result.IsValid);
        Assert.Contains("duplicate-pair", result.Errors);
    }

    // ── Builder helpers ────────────────────────────────────────────────────────

    private static EntityStateDto MakeMinimalEntity(string id, string shortId, string name) => new()
    {
        Id         = id,
        ShortId    = shortId,
        Name       = name,
        Species    = SpeciesType.Human,
        Position   = new PositionStateDto { HasPosition = false },
        Drives     = new DrivesStateDto(),
        Physiology = new PhysiologyStateDto { BodyTemp = 36.5f, Satiation = 50f, Hydration = 50f, Energy = 50f }
    };

    private static WorldStateDto MakeMinimalWorldState(
        List<EntityStateDto>    entities,
        List<RelationshipDto>?  relationships = null,
        List<MemoryEventDto>?   memoryEvents  = null) => new()
    {
        SchemaVersion = "0.2.0",
        CapturedAt    = new DateTimeOffset(2026, 4, 24, 10, 0, 0, TimeSpan.Zero),
        Tick          = 1,
        Clock         = new ClockStateDto
        {
            GameTimeDisplay = "Day 1 10:00", DayNumber = 1,
            IsDaytime = true, CircadianFactor = 0.5f, TimeScale = 1.0f
        },
        Entities      = entities,
        WorldItems    = new List<WorldItemDto>(),
        WorldObjects  = new List<WorldObjectDto>(),
        Invariants    = new InvariantDigestDto { ViolationCount = 0 },
        Relationships = relationships,
        MemoryEvents  = memoryEvents
    };
}
