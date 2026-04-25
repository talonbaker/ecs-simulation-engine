using System;
using System.Text;
using Warden.Contracts.SchemaValidation;
using Xunit;

namespace Warden.Contracts.Tests;

/// <summary>
/// Unit tests for <see cref="SchemaValidator"/>, covering:
///   AT-02 — Required field missing / null → validation error
///   AT-03 — additionalProperties:false violation → validation error
///   AT-04 — maxItems overflow (26 scenarios) → validation error
///   AT-05 — Unknown schema keyword → <see cref="NotSupportedException"/> at load time
/// </summary>
public class SchemaValidatorTests
{
    // ── AT-02: required field missing ─────────────────────────────────────────

    [Fact]
    public void Validate_MissingRequiredField_ReturnsError()
    {
        // WorldState requires "schemaVersion" — omit it entirely
        const string json = """
            {
              "capturedAt": "2024-01-01T00:00:00+00:00",
              "tick": 0,
              "clock": {
                "gameTimeDisplay": "Day 0",
                "dayNumber": 0,
                "isDaytime": true,
                "circadianFactor": 0.5,
                "timeScale": 1.0
              },
              "entities": [],
              "worldItems": [],
              "worldObjects": [],
              "invariants": { "violationCount": 0 }
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("schemaVersion"));
    }

    [Fact]
    public void Validate_MissingMultipleRequiredFields_ReportsEachMissing()
    {
        // Minimal object missing tick, clock, entities, worldItems, worldObjects, invariants
        const string json = """{"schemaVersion":"0.1.0","capturedAt":"2024-01-01T00:00:00+00:00"}""";

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("tick"));
        Assert.Contains(result.Errors, e => e.Contains("clock"));
    }

    [Fact]
    public void Validate_EmptyCommandsArray_FailsMinItems()
    {
        // AiCommandBatch.commands has minItems: 1 — empty array violates it
        const string json = """{"schemaVersion":"0.1.0","commands":[]}""";

        var result = SchemaValidator.Validate(json, Schema.AiCommandBatch);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    // ── AT-03: additionalProperties:false violation ───────────────────────────

    [Fact]
    public void Validate_AdditionalProperty_AtRoot_ReturnsError()
    {
        // WorldState has additionalProperties:false at the root
        const string json = """
            {
              "schemaVersion": "0.1.0",
              "capturedAt": "2024-01-01T00:00:00+00:00",
              "tick": 0,
              "clock": {
                "gameTimeDisplay": "Day 0",
                "dayNumber": 0,
                "isDaytime": true,
                "circadianFactor": 0.5,
                "timeScale": 1.0
              },
              "entities": [],
              "worldItems": [],
              "worldObjects": [],
              "invariants": { "violationCount": 0 },
              "bogusExtraField": "this-is-not-allowed"
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("bogusExtraField"));
    }

    [Fact]
    public void Validate_AdditionalProperty_InNestedObject_ReturnsError()
    {
        // The "clock" object also has additionalProperties:false
        const string json = """
            {
              "schemaVersion": "0.1.0",
              "capturedAt": "2024-01-01T00:00:00+00:00",
              "tick": 0,
              "clock": {
                "gameTimeDisplay": "Day 0",
                "dayNumber": 0,
                "isDaytime": true,
                "circadianFactor": 0.5,
                "timeScale": 1.0,
                "undocumentedClockField": 99
              },
              "entities": [],
              "worldItems": [],
              "worldObjects": [],
              "invariants": { "violationCount": 0 }
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("undocumentedClockField"));
    }

    // ── AT-04: maxItems overflow ───────────────────────────────────────────────

    [Fact]
    public void Validate_ScenarioBatch_TwentySixScenarios_ExceedsMaxItems()
    {
        // sonnet-to-haiku schema: scenarios maxItems == 25; supply 26
        var sb = new StringBuilder();
        sb.Append(@"{""schemaVersion"":""0.1.0"",""batchId"":""batch-overflow-test"",");
        sb.Append(@"""parentSpecId"":""spec-test-01"",""scenarios"":[");

        for (int i = 1; i <= 26; i++)
        {
            if (i > 1) sb.Append(',');
            var id = i < 10 ? $"sc-0{i}" : $"sc-{i}";
            sb.Append($@"{{""scenarioId"":""{id}"",""seed"":{i},""durationGameSeconds"":60,");
            sb.Append(@"""assertions"":[{""id"":""A-01"",""kind"":""at-end"",""target"":""entities[0].physiology.satiation""}]}");
        }

        sb.Append("]}");

        var result = SchemaValidator.Validate(sb.ToString(), Schema.SonnetToHaiku);

        Assert.False(result.IsValid);
        // Error message should mention actual count (26) and the limit (25)
        Assert.Contains(result.Errors, e => e.Contains("26") && e.Contains("25"));
    }

    [Fact]
    public void Validate_ScenarioBatch_ExactlyTwentyFiveScenarios_IsValid()
    {
        // Boundary: exactly 25 scenarios must pass
        var sb = new StringBuilder();
        sb.Append(@"{""schemaVersion"":""0.1.0"",""batchId"":""batch-boundary"",");
        sb.Append(@"""parentSpecId"":""spec-test-01"",""scenarios"":[");

        for (int i = 1; i <= 25; i++)
        {
            if (i > 1) sb.Append(',');
            var id = i < 10 ? $"sc-0{i}" : $"sc-{i}";
            sb.Append($@"{{""scenarioId"":""{id}"",""seed"":{i},""durationGameSeconds"":60,");
            sb.Append(@"""assertions"":[{""id"":""A-01"",""kind"":""at-end"",""target"":""entities[0].physiology.satiation""}]}");
        }

        sb.Append("]}");

        var result = SchemaValidator.Validate(sb.ToString(), Schema.SonnetToHaiku);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    // ── AT-05: unknown schema keyword → NotSupportedException ─────────────────

    [Fact]
    public void ValidateWithSchema_UnknownKeyword_ThrowsNotSupportedException()
    {
        const string schemaJson = """
            {
              "type": "object",
              "unknownFutureKeyword": "this keyword is not in the supported set"
            }
            """;

        var ex = Assert.Throws<NotSupportedException>(() =>
            SchemaValidator.ValidateWithSchema("{}", schemaJson));

        Assert.Contains("unknownFutureKeyword", ex.Message);
    }

    [Fact]
    public void ValidateWithSchema_UnknownKeyword_InNestedProperty_ThrowsNotSupportedException()
    {
        // The unknown keyword is nested inside a properties sub-schema, not at root
        const string schemaJson = """
            {
              "type": "object",
              "properties": {
                "name": {
                  "type": "string",
                  "deprecated": true
                }
              }
            }
            """;

        Assert.Throws<NotSupportedException>(() =>
            SchemaValidator.ValidateWithSchema(@"{""name"":""test""}", schemaJson));
    }

    [Fact]
    public void ValidateWithSchema_KnownKeywords_DoesNotThrow()
    {
        // All keywords here are in the known set — must not throw
        const string schemaJson = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "required": ["id"],
              "properties": {
                "id": { "type": "string", "minLength": 1, "maxLength": 64 },
                "count": { "type": "integer", "minimum": 0, "maximum": 100 }
              },
              "additionalProperties": false
            }
            """;

        // Should not throw; result should report valid
        var result = SchemaValidator.ValidateWithSchema(@"{""id"":""abc""}", schemaJson);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    // ── Extra coverage: happy paths and edge cases ─────────────────────────────

    [Fact]
    public void Validate_ValidAiCommandBatch_SpawnFood_ReturnsOk()
    {
        const string json = """
            {
              "schemaVersion": "0.1.0",
              "commands": [
                { "kind": "spawn-food", "foodType": "banana", "x": 5.0, "y": 0.0, "z": 5.0 }
              ]
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.AiCommandBatch);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Validate_HaikuResult_BlockedWithoutBlockReason_ReturnsError()
    {
        // allOf if/then: when outcome == "blocked", blockReason is required
        const string json = """
            {
              "schemaVersion": "0.1.0",
              "scenarioId": "sc-01",
              "parentBatchId": "batch-abc-123",
              "workerId": "haiku-01",
              "outcome": "blocked",
              "assertionResults": [{ "id": "A-01", "passed": false }],
              "tokensUsed": { "input": 100, "cachedRead": 50, "output": 20 }
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.HaikuResult);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("blockReason"));
    }

    [Fact]
    public void Validate_SonnetResult_BlockedWithoutBlockReason_ReturnsError()
    {
        const string json = """
            {
              "schemaVersion": "0.1.0",
              "specId": "spec-test-01",
              "workerId": "sonnet-01",
              "outcome": "blocked",
              "acceptanceTestResults": [{ "id": "AT-01", "passed": false }],
              "tokensUsed": { "input": 500, "cachedRead": 0, "output": 50 }
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.SonnetResult);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("blockReason"));
    }

    [Fact]
    public void Validate_MalformedJson_ReturnsParseError()
    {
        var result = SchemaValidator.Validate("{not: valid json", Schema.WorldState);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.StartsWith("JSON parse error"));
    }

    [Fact]
    public void ValidationResult_Ok_Singleton_IsReused()
    {
        // Validate something valid twice; the Ok singleton should be returned both times
        const string json = """
            {
              "schemaVersion": "0.1.0",
              "commands": [
                { "kind": "spawn-food", "foodType": "banana", "x": 1.0, "y": 0.0, "z": 1.0 }
              ]
            }
            """;

        var r1 = SchemaValidator.Validate(json, Schema.AiCommandBatch);
        var r2 = SchemaValidator.Validate(json, Schema.AiCommandBatch);

        Assert.True(r1.IsValid);
        Assert.True(r2.IsValid);
        Assert.Same(ValidationResult.Ok, r1);
        Assert.Same(ValidationResult.Ok, r2);
    }
}
