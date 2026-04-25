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

    // ── v0.2 schema compatibility (AT-02) ─────────────────────────────────────

    /// <summary>
    /// AT-02 — The pre-existing v0.1 sample round-trips under the v0.2.1 schema.
    /// Additive compatibility: v0.2.1 schema must accept v0.1 messages by treating
    /// new optional fields as absent.
    /// </summary>
    [Fact]
    public void WorldState_V01SampleRoundTripsUnderV021Schema() =>
        AssertRoundTrip<WorldStateDto>("world-state.sample.json", Schema.WorldState);

    // ── v0.2.1 schema round-trip tests ────────────────────────────────────────

    /// <summary>
    /// AT-03 — The canonical v0.2.1 sample round-trips clean.
    /// </summary>
    [Fact]
    public void WorldState_V021SampleRoundTrips() =>
        AssertRoundTrip<WorldStateDto>("world-state-v021.json", Schema.WorldState);

    /// <summary>
    /// AT-04 — drives.belonging.current = 101 is rejected with a maximum error.
    /// </summary>
    [Fact]
    public void WorldState_V021_DriveCurrentOver100_FailsMaximum()
    {
        const string json = """
            {
              "schemaVersion": "0.2.1",
              "capturedAt": "2026-04-25T10:00:00+00:00",
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
                                "energy": 50, "sleepiness": 50, "isSleeping": false },
                "social": {
                  "drives": {
                    "belonging":  { "current": 101, "baseline": 50 },
                    "status":     { "current": 40, "baseline": 40 },
                    "affection":  { "current": 40, "baseline": 40 },
                    "irritation": { "current": 40, "baseline": 40 },
                    "attraction": { "current": 40, "baseline": 40 },
                    "trust":      { "current": 40, "baseline": 40 },
                    "suspicion":  { "current": 40, "baseline": 40 },
                    "loneliness": { "current": 40, "baseline": 40 }
                  }
                }
              }],
              "worldItems": [], "worldObjects": [],
              "invariants": { "violationCount": 0 }
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("maximum"));
    }

    /// <summary>
    /// AT-05 — willpower.baseline = -1 is rejected with a minimum error.
    /// </summary>
    [Fact]
    public void WorldState_V021_WillpowerBaselineNegative_FailsMinimum()
    {
        const string json = """
            {
              "schemaVersion": "0.2.1",
              "capturedAt": "2026-04-25T10:00:00+00:00",
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
                                "energy": 50, "sleepiness": 50, "isSleeping": false },
                "social": {
                  "willpower": { "current": 50, "baseline": -1 }
                }
              }],
              "worldItems": [], "worldObjects": [],
              "invariants": { "violationCount": 0 }
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("minimum"));
    }

    /// <summary>
    /// AT-06 — inhibitions[].class with an unknown value is rejected with an enum error.
    /// </summary>
    [Fact]
    public void WorldState_V021_InhibitionBadClass_FailsEnum()
    {
        const string json = """
            {
              "schemaVersion": "0.2.1",
              "capturedAt": "2026-04-25T10:00:00+00:00",
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
                                "energy": 50, "sleepiness": 50, "isSleeping": false },
                "social": {
                  "inhibitions": [
                    { "class": "notARealClass", "strength": 50, "awareness": "known" }
                  ]
                }
              }],
              "worldItems": [], "worldObjects": [],
              "invariants": { "violationCount": 0 }
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("enum"));
    }

    /// <summary>
    /// AT-07 — Nine inhibition entries fail maxItems: 8.
    /// </summary>
    [Fact]
    public void WorldState_V021_NineInhibitions_FailsMaxItems()
    {
        const string json = """
            {
              "schemaVersion": "0.2.1",
              "capturedAt": "2026-04-25T10:00:00+00:00",
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
                                "energy": 50, "sleepiness": 50, "isSleeping": false },
                "social": {
                  "inhibitions": [
                    { "class": "infidelity",           "strength": 10, "awareness": "known" },
                    { "class": "confrontation",        "strength": 20, "awareness": "known" },
                    { "class": "bodyImageEating",      "strength": 30, "awareness": "hidden" },
                    { "class": "publicEmotion",        "strength": 40, "awareness": "known" },
                    { "class": "physicalIntimacy",     "strength": 50, "awareness": "hidden" },
                    { "class": "interpersonalConflict","strength": 60, "awareness": "known" },
                    { "class": "riskTaking",           "strength": 70, "awareness": "hidden" },
                    { "class": "vulnerability",        "strength": 80, "awareness": "known" },
                    { "class": "confrontation",        "strength": 90, "awareness": "hidden" }
                  ]
                }
              }],
              "worldItems": [], "worldObjects": [],
              "invariants": { "violationCount": 0 }
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("array has 9") && e.Contains("maximum is 8"));
    }

    /// <summary>
    /// AT-08 — An inhibition with awareness "hidden" round-trips clean.
    /// </summary>
    [Fact]
    public void WorldState_V021_InhibitionHiddenAwareness_RoundTripsClean()
    {
        var entity = MakeMinimalEntity("11111111-1111-1111-1111-111111111111", "E1", "Alice");
        var entityWithInhibition = entity with
        {
            Social = new SocialStateDto
            {
                Inhibitions = new List<InhibitionDto>
                {
                    new() { Class = InhibitionClass.Vulnerability, Strength = 90, Awareness = InhibitionAwareness.Hidden }
                }
            }
        };

        var dto  = MakeMinimalWorldState(new List<EntityStateDto> { entityWithInhibition });
        var json = JsonSerializer.Serialize(dto, JsonOptions.Wire);

        var v = SchemaValidator.Validate(json, Schema.WorldState);
        Assert.True(v.IsValid, string.Join("; ", v.Errors));

        var dto2  = JsonSerializer.Deserialize<WorldStateDto>(json, JsonOptions.Wire)!;
        var json2 = JsonSerializer.Serialize(dto2, JsonOptions.Wire);
        Assert.Equal(json, json2);

        var social = dto2.Entities[0].Social;
        Assert.NotNull(social);
        Assert.NotNull(social!.Inhibitions);
        Assert.Single(social.Inhibitions!);
        Assert.Equal(InhibitionAwareness.Hidden, social.Inhibitions![0].Awareness);
    }

    /// <summary>
    /// AT-09 — A drives object missing one of the eight required sub-fields is rejected.
    /// </summary>
    [Fact]
    public void WorldState_V021_DriveMissingSubField_FailsRequired()
    {
        const string json = """
            {
              "schemaVersion": "0.2.1",
              "capturedAt": "2026-04-25T10:00:00+00:00",
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
                                "energy": 50, "sleepiness": 50, "isSleeping": false },
                "social": {
                  "drives": {
                    "belonging":  { "current": 50, "baseline": 50 },
                    "status":     { "current": 50, "baseline": 50 },
                    "affection":  { "current": 50, "baseline": 50 },
                    "irritation": { "current": 50, "baseline": 50 },
                    "attraction": { "current": 50, "baseline": 50 },
                    "trust":      { "current": 50, "baseline": 50 },
                    "suspicion":  { "current": 50, "baseline": 50 }
                  }
                }
              }],
              "worldItems": [], "worldObjects": [],
              "invariants": { "violationCount": 0 }
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("loneliness"));
    }

    /// <summary>
    /// AT-10 — A relationship with pairDrives is rejected by additionalProperties:false.
    /// </summary>
    [Fact]
    public void WorldState_V021_RelationshipPairDrives_RejectedByAdditionalProperties()
    {
        const string json = """
            {
              "schemaVersion": "0.2.1",
              "capturedAt": "2026-04-25T10:00:00+00:00",
              "tick": 1,
              "clock": {
                "gameTimeDisplay": "t", "dayNumber": 1, "isDaytime": true,
                "circadianFactor": 0.5, "timeScale": 1.0
              },
              "entities": [],
              "worldItems": [], "worldObjects": [],
              "invariants": { "violationCount": 0 },
              "relationships": [{
                "id": "aaaa0000-0000-0000-0000-000000000000",
                "participantA": "11111111-1111-1111-1111-111111111111",
                "participantB": "22222222-2222-2222-2222-222222222222",
                "patterns": [],
                "pairDrives": { "attraction": 10, "trust": 10, "suspicion": 10, "jealousy": 10 },
                "intensity": 50,
                "historyEventIds": []
              }]
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("pairDrives"));
    }

    /// <summary>
    /// AT-11 — The DTO assembly contains no SelfDrivesDto, PairDrivesDto, or Jealousy field.
    /// </summary>
    [Fact]
    public void DtoGraph_ContainsNo_SelfDrivesDto_PairDrivesDto_JealousyField()
    {
        var assembly = typeof(WorldStateDto).Assembly;

        Assert.Null(assembly.GetType("Warden.Contracts.Telemetry.SelfDrivesDto"));
        Assert.Null(assembly.GetType("Warden.Contracts.Telemetry.PairDrivesDto"));

        var drivesDto = assembly.GetType("Warden.Contracts.Telemetry.DrivesDto");
        Assert.NotNull(drivesDto);
        Assert.Null(drivesDto!.GetProperty("Jealousy"));
    }

    // ── v0.2.1 referential checker tests ──────────────────────────────────────

    /// <summary>
    /// A memory event with scope "global" is rejected by the referential
    /// checker with the specific reason "global-scope-reserved-for-v0.3".
    /// </summary>
    [Fact]
    public void WorldState_V021_GlobalMemoryScope_RejectedByReferentialChecker()
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
    /// A relationship whose participantA is not in entities[] is rejected
    /// by the referential checker with a reason naming the missing id.
    /// </summary>
    [Fact]
    public void WorldState_V021_RelationshipParticipantMissing_RejectedByReferentialChecker()
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
                    Intensity       = 0,
                    HistoryEventIds = Array.Empty<string>()
                }
            });

        var result = WorldStateReferentialChecker.Check(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not found in entities"));
    }

    /// <summary>
    /// Two relationships sharing the same unordered pair (A,B) and (B,A)
    /// are rejected with reason "duplicate-pair".
    /// </summary>
    [Fact]
    public void WorldState_V021_DuplicateUnorderedPair_RejectedByReferentialChecker()
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
                    Intensity = 0,
                    HistoryEventIds = Array.Empty<string>()
                },
                new()
                {
                    // Same pair, reversed — (B,A) duplicates (A,B)
                    Id = "aaaa0002-0000-0000-0000-000000000000",
                    ParticipantA = b.Id, ParticipantB = a.Id,
                    Patterns = Array.Empty<RelationshipPattern>(),
                    Intensity = 0,
                    HistoryEventIds = Array.Empty<string>()
                }
            });

        var result = WorldStateReferentialChecker.Check(dto);

        Assert.False(result.IsValid);
        Assert.Contains("duplicate-pair", result.Errors);
    }

    // ── Existing negative tests (schema-level) ─────────────────────────────────

    /// <summary>
    /// A relationship with three patterns fails maxItems: 2.
    /// </summary>
    [Fact]
    public void WorldState_V021_RelationshipThreePatterns_FailsMaxItems()
    {
        const string json = """
            {
              "schemaVersion": "0.2.1",
              "capturedAt": "2026-04-25T10:00:00+00:00",
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
    /// A memory event description of 281 characters fails maxLength: 280.
    /// </summary>
    [Fact]
    public void WorldState_V021_MemoryEventDescriptionTooLong_FailsMaxLength()
    {
        var longDescription = new string('x', 281);
        var json = $$"""
            {
              "schemaVersion": "0.2.1",
              "capturedAt": "2026-04-25T10:00:00+00:00",
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
        SchemaVersion = "0.2.1",
        CapturedAt    = new DateTimeOffset(2026, 4, 25, 10, 0, 0, TimeSpan.Zero),
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
