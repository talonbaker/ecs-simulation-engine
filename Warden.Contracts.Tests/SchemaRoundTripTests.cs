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

    // ── v0.3 round-trip tests ──────────────────────────────────────────────────

    /// <summary>
    /// AT-04 (v0.3) — The canonical v0.3 sample round-trips clean: schema validates,
    /// DTO deserialises, re-serialises to JSON semantically equal to the input.
    /// </summary>
    [Fact]
    public void WorldState_V03SampleRoundTrips() =>
        AssertRoundTrip<WorldStateDto>("world-state-v030.json", Schema.WorldState);

    /// <summary>
    /// AT-03 (v0.3) — The v0.2.1 sample round-trips clean under the v0.3 schema.
    /// Additive compatibility holds across two minor bumps.
    /// </summary>
    [Fact]
    public void WorldState_V021SampleRoundTripsUnderV03Schema() =>
        AssertRoundTrip<WorldStateDto>("world-state-v021.json", Schema.WorldState);

    /// <summary>
    /// AT-02 (v0.3) — The v0.1 sample round-trips clean under the v0.3 schema.
    /// </summary>
    [Fact]
    public void WorldState_V01SampleRoundTripsUnderV03Schema() =>
        AssertRoundTrip<WorldStateDto>("world-state.sample.json", Schema.WorldState);

    /// <summary>
    /// AT-05 — lightSources[].roomId pointing to a non-existent room id is rejected
    /// with reason "light-source-room-missing".
    /// </summary>
    [Fact]
    public void WorldState_V03_LightSourceMissingRoom_RejectedByReferentialChecker()
    {
        var dto = MakeMinimalWorldState(
            new List<EntityStateDto>(),
            lightSources: new List<LightSourceDto>
            {
                new()
                {
                    Id               = "bbbbbbbb-0001-0000-0000-000000000000",
                    Kind             = LightKind.DeskLamp,
                    State            = LightState.On,
                    Intensity        = 50,
                    ColorTemperatureK = 3000,
                    Position         = new TilePointDto { X = 5, Y = 5 },
                    RoomId           = "aaaaaaaa-9999-0000-0000-000000000000" // does not exist
                }
            });

        var result = WorldStateReferentialChecker.Check(dto);

        Assert.False(result.IsValid);
        Assert.Contains("light-source-room-missing", result.Errors);
    }

    /// <summary>
    /// AT-06 — lightApertures[].roomId pointing to a non-existent room id is rejected
    /// with reason "aperture-room-missing".
    /// </summary>
    [Fact]
    public void WorldState_V03_ApertureMissingRoom_RejectedByReferentialChecker()
    {
        var dto = MakeMinimalWorldState(
            new List<EntityStateDto>(),
            lightApertures: new List<LightApertureDto>
            {
                new()
                {
                    Id          = "cccccccc-0001-0000-0000-000000000000",
                    Position    = new TilePointDto { X = 2, Y = 5 },
                    RoomId      = "aaaaaaaa-9999-0000-0000-000000000000", // does not exist
                    Facing      = ApertureFacing.South,
                    AreaSqTiles = 3.0
                }
            });

        var result = WorldStateReferentialChecker.Check(dto);

        Assert.False(result.IsValid);
        Assert.Contains("aperture-room-missing", result.Errors);
    }

    /// <summary>
    /// AT-07 — rooms[].illumination.dominantSourceId pointing to a non-existent
    /// light source is rejected with reason "dominant-source-missing".
    /// </summary>
    [Fact]
    public void WorldState_V03_DominantSourceMissing_RejectedByReferentialChecker()
    {
        var dto = MakeMinimalWorldState(
            new List<EntityStateDto>(),
            rooms: new List<RoomDto>
            {
                new()
                {
                    Id          = "aaaaaaaa-0001-0000-0000-000000000000",
                    Name        = "breakroom",
                    Category    = RoomCategory.Breakroom,
                    Floor       = BuildingFloor.First,
                    BoundsRect  = new BoundsRectDto { X = 0, Y = 0, Width = 5, Height = 5 },
                    Illumination = new IlluminationDto
                    {
                        AmbientLevel      = 50,
                        ColorTemperatureK = 4000,
                        DominantSourceId  = "bbbbbbbb-9999-0000-0000-000000000000" // does not exist
                    }
                }
            });

        var result = WorldStateReferentialChecker.Check(dto);

        Assert.False(result.IsValid);
        Assert.Contains("dominant-source-missing", result.Errors);
    }

    /// <summary>
    /// AT-08 — Two rooms[] entries sharing an id are rejected with reason "duplicate-room-id".
    /// </summary>
    [Fact]
    public void WorldState_V03_DuplicateRoomId_RejectedByReferentialChecker()
    {
        var sharedId = "aaaaaaaa-0001-0000-0000-000000000000";
        var dto = MakeMinimalWorldState(
            new List<EntityStateDto>(),
            rooms: new List<RoomDto>
            {
                new()
                {
                    Id          = sharedId,
                    Name        = "breakroom",
                    Category    = RoomCategory.Breakroom,
                    Floor       = BuildingFloor.First,
                    BoundsRect  = new BoundsRectDto { X = 0, Y = 0, Width = 5, Height = 5 },
                    Illumination = new IlluminationDto { AmbientLevel = 50, ColorTemperatureK = 4000 }
                },
                new()
                {
                    Id          = sharedId, // same id — duplicate
                    Name        = "office",
                    Category    = RoomCategory.Office,
                    Floor       = BuildingFloor.First,
                    BoundsRect  = new BoundsRectDto { X = 10, Y = 0, Width = 4, Height = 4 },
                    Illumination = new IlluminationDto { AmbientLevel = 70, ColorTemperatureK = 3500 }
                }
            });

        var result = WorldStateReferentialChecker.Check(dto);

        Assert.False(result.IsValid);
        Assert.Contains("duplicate-room-id", result.Errors);
    }

    /// <summary>
    /// AT-09 — rooms[].boundsRect.width = 0 is rejected by schema minimum: 1.
    /// </summary>
    [Fact]
    public void WorldState_V03_BoundsRectWidthZero_FailsMinimum()
    {
        const string json = """
            {
              "schemaVersion": "0.3.0",
              "capturedAt": "2026-04-25T09:30:00+00:00",
              "tick": 1,
              "clock": {
                "gameTimeDisplay": "t", "dayNumber": 1, "isDaytime": true,
                "circadianFactor": 0.5, "timeScale": 1.0
              },
              "entities": [],
              "worldItems": [],
              "worldObjects": [],
              "rooms": [{
                "id": "aaaaaaaa-0001-0000-0000-000000000000",
                "name": "test-room",
                "category": "breakroom",
                "floor": "first",
                "boundsRect": { "x": 0, "y": 0, "width": 0, "height": 5 },
                "illumination": { "ambientLevel": 50, "colorTemperatureK": 4000 }
              }],
              "invariants": { "violationCount": 0 }
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("minimum"));
    }

    /// <summary>
    /// AT-10 — clock.sun.elevationDeg = 91 is rejected by schema maximum: 90.
    /// </summary>
    [Fact]
    public void WorldState_V03_SunElevationDegTooHigh_FailsMaximum()
    {
        const string json = """
            {
              "schemaVersion": "0.3.0",
              "capturedAt": "2026-04-25T09:30:00+00:00",
              "tick": 1,
              "clock": {
                "gameTimeDisplay": "t", "dayNumber": 1, "isDaytime": true,
                "circadianFactor": 0.5, "timeScale": 1.0,
                "sun": {
                  "azimuthDeg": 180.0,
                  "elevationDeg": 91,
                  "dayPhase": "afternoon"
                }
              },
              "entities": [],
              "worldItems": [],
              "worldObjects": [],
              "invariants": { "violationCount": 0 }
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("maximum"));
    }

    /// <summary>
    /// AT-11 — A v0.3 document with no clock.sun validates and round-trips clean.
    /// </summary>
    [Fact]
    public void WorldState_V03_AbsentSun_ValidatesClean()
    {
        var dto = MakeMinimalWorldState(new List<EntityStateDto>());
        var json = JsonSerializer.Serialize(dto with { SchemaVersion = "0.3.0" }, JsonOptions.Wire);

        var v = SchemaValidator.Validate(json, Schema.WorldState);
        Assert.True(v.IsValid, string.Join("; ", v.Errors));
    }

    /// <summary>
    /// AT-12 — lightSources[].state = "burned-out" is rejected by enum
    /// (only on, off, flickering, dying are valid).
    /// </summary>
    [Fact]
    public void WorldState_V03_LightSourceInvalidState_FailsEnum()
    {
        const string json = """
            {
              "schemaVersion": "0.3.0",
              "capturedAt": "2026-04-25T09:30:00+00:00",
              "tick": 1,
              "clock": {
                "gameTimeDisplay": "t", "dayNumber": 1, "isDaytime": true,
                "circadianFactor": 0.5, "timeScale": 1.0
              },
              "entities": [],
              "worldItems": [],
              "worldObjects": [],
              "rooms": [{
                "id": "aaaaaaaa-0001-0000-0000-000000000000",
                "name": "test-room",
                "category": "breakroom",
                "floor": "first",
                "boundsRect": { "x": 0, "y": 0, "width": 5, "height": 5 },
                "illumination": { "ambientLevel": 50, "colorTemperatureK": 4000 }
              }],
              "lightSources": [{
                "id": "bbbbbbbb-0001-0000-0000-000000000000",
                "kind": "overheadFluorescent",
                "state": "burned-out",
                "intensity": 0,
                "colorTemperatureK": 4500,
                "position": { "x": 5, "y": 5 },
                "roomId": "aaaaaaaa-0001-0000-0000-000000000000"
              }],
              "invariants": { "violationCount": 0 }
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("enum"));
    }

    /// <summary>
    /// AT-13 — Two rooms with overlapping boundsRect validate fine.
    /// Overlapping bounds are physically real (hallway passing under balcony)
    /// and are not a referential error.
    /// </summary>
    [Fact]
    public void WorldState_V03_OverlappingRoomBounds_ValidatesClean()
    {
        var dto = MakeMinimalWorldState(
            new List<EntityStateDto>(),
            rooms: new List<RoomDto>
            {
                new()
                {
                    Id          = "aaaaaaaa-0001-0000-0000-000000000000",
                    Name        = "hallway",
                    Category    = RoomCategory.Hallway,
                    Floor       = BuildingFloor.First,
                    BoundsRect  = new BoundsRectDto { X = 0, Y = 0, Width = 20, Height = 3 },
                    Illumination = new IlluminationDto { AmbientLevel = 40, ColorTemperatureK = 4000 }
                },
                new()
                {
                    Id          = "aaaaaaaa-0002-0000-0000-000000000000",
                    Name        = "office",
                    Category    = RoomCategory.Office,
                    Floor       = BuildingFloor.First,
                    BoundsRect  = new BoundsRectDto { X = 5, Y = 1, Width = 6, Height = 6 }, // overlaps hallway
                    Illumination = new IlluminationDto { AmbientLevel = 70, ColorTemperatureK = 3500 }
                }
            });

        var json = JsonSerializer.Serialize(dto with { SchemaVersion = "0.3.0" }, JsonOptions.Wire);

        var schemaResult = SchemaValidator.Validate(json, Schema.WorldState);
        Assert.True(schemaResult.IsValid, string.Join("; ", schemaResult.Errors));

        var refResult = WorldStateReferentialChecker.Check(
            JsonSerializer.Deserialize<WorldStateDto>(json, JsonOptions.Wire)!);
        Assert.True(refResult.IsValid, string.Join("; ", refResult.Errors));
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
        List<EntityStateDto>          entities,
        List<RelationshipDto>?        relationships  = null,
        List<MemoryEventDto>?         memoryEvents   = null,
        List<RoomDto>?                rooms          = null,
        List<LightSourceDto>?         lightSources   = null,
        List<LightApertureDto>?       lightApertures = null,
        List<ChronicleEntryDto>?      chronicle      = null) => new()
    {
        SchemaVersion  = "0.4.0",
        CapturedAt     = new DateTimeOffset(2026, 4, 25, 10, 0, 0, TimeSpan.Zero),
        Tick           = 1,
        Clock          = new ClockStateDto
        {
            GameTimeDisplay = "Day 1 10:00", DayNumber = 1,
            IsDaytime = true, CircadianFactor = 0.5f, TimeScale = 1.0f
        },
        Entities       = entities,
        WorldItems     = new List<WorldItemDto>(),
        WorldObjects   = new List<WorldObjectDto>(),
        Invariants     = new InvariantDigestDto { ViolationCount = 0 },
        Relationships  = relationships,
        MemoryEvents   = memoryEvents,
        Rooms          = rooms,
        LightSources   = lightSources,
        LightApertures = lightApertures,
        Chronicle      = chronicle
    };

    // ── v0.4 round-trip tests ──────────────────────────────────────────────────

    /// <summary>
    /// AT-01 (v0.4) — The schema declares schemaVersion enum including "0.4.0"
    /// and the chronicle[] field with maxItems: 4096.
    /// </summary>
    [Fact]
    public void WorldState_V04_SchemaHas040EnumAndChronicleArray()
    {
        // A minimal "0.4.0" document with one chronicle entry validates clean.
        var dto = MakeMinimalWorldState(
            new List<EntityStateDto>(),
            chronicle: new List<ChronicleEntryDto>
            {
                new()
                {
                    Id           = "aaaa0010-0000-0000-0000-000000000000",
                    Kind         = ChronicleEventKind.SpilledSomething,
                    Tick         = 1L,
                    Participants = new List<string>(),
                    Description  = "A spill.",
                    Persistent   = true,
                }
            });

        var json = JsonSerializer.Serialize(dto, JsonOptions.Wire);
        var v    = SchemaValidator.Validate(json, Schema.WorldState);
        Assert.True(v.IsValid, $"Schema validation failed: {string.Join("; ", v.Errors)}\n\nJSON:\n{json}");
    }

    /// <summary>
    /// AT-02 (v0.4) — A v0.3 sample (no chronicle) round-trips clean under v0.4 schema.
    /// Additive compatibility holds.
    /// </summary>
    [Fact]
    public void WorldState_V03SampleRoundTripsUnderV04Schema() =>
        AssertRoundTrip<WorldStateDto>("world-state-v030.json", Schema.WorldState);

    /// <summary>
    /// AT-03 (v0.4) — The canonical v0.4 sample with 3 chronicle entries round-trips clean.
    /// </summary>
    [Fact]
    public void WorldState_V04SampleRoundTrips() =>
        AssertRoundTrip<WorldStateDto>("world-state-v040.json", Schema.WorldState);

    /// <summary>
    /// AT-04 (v0.4) — chronicle[].kind with an unknown value is rejected by enum validation.
    /// </summary>
    [Fact]
    public void WorldState_V04_ChronicleUnknownKind_FailsEnum()
    {
        const string json = """
            {
              "schemaVersion": "0.4.0",
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
              "chronicle": [{
                "id": "aaaa0020-0000-0000-0000-000000000000",
                "kind": "notARealKind",
                "tick": 1,
                "participants": [],
                "description": "test",
                "persistent": true
              }]
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("enum"));
    }

    /// <summary>
    /// AT-05 (v0.4) — chronicle[] with 4097 entries fails maxItems: 4096.
    /// </summary>
    [Fact]
    public void WorldState_V04_ChronicleOverMaxItems_Fails()
    {
        var entries = new System.Text.StringBuilder();
        for (int i = 0; i < 4097; i++)
        {
            if (i > 0) entries.Append(',');
            entries.Append($$$"""
                {
                  "id": "aaaa{{{i:D4}}}-0000-0000-0000-000000000000",
                  "kind": "other",
                  "tick": {{{i}}},
                  "participants": [],
                  "description": "entry",
                  "persistent": true
                }
                """);
        }

        var json = $$"""
            {
              "schemaVersion": "0.4.0",
              "capturedAt": "2026-04-25T10:00:00+00:00",
              "tick": 1,
              "clock": {
                "gameTimeDisplay": "t", "dayNumber": 1, "isDaytime": true,
                "circadianFactor": 0.5, "timeScale": 1.0
              },
              "entities": [], "worldItems": [], "worldObjects": [],
              "invariants": { "violationCount": 0 },
              "chronicle": [{{entries}}]
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("4097") && e.Contains("4096"));
    }
}
