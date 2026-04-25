using System.IO;
using System.Reflection;
using Warden.Contracts.SchemaValidation;
using Xunit;

namespace Warden.Contracts.Tests.SchemaValidation;

/// <summary>
/// Schema-validation tests for world-definition.schema.json.
///
/// AT-01 — Schema declares schemaVersion "0.1.0" and enforces additionalProperties:false,
///          maxItems on all arrays, and bounds on all numerics.
/// AT-02 — Starter file validates clean against the schema.
/// AT-06 — Malformed files are rejected with messages that include the failing path.
/// AT-07 — Free archetypeHint value validates clean.
/// </summary>
public class WorldDefinitionSchemaTests
{
    private const string MinimalValid = """
        {
          "schemaVersion": "0.1.0",
          "worldId": "test",
          "name": "Test World",
          "seed": 1,
          "floors": [],
          "rooms": [],
          "lightSources": [],
          "lightApertures": [],
          "npcSlots": [],
          "objectsAtAnchors": []
        }
        """;

    // ── AT-01: schema structural discipline ──────────────────────────────────

    [Fact]
    public void Schema_CanBeLoaded_WithoutUnsupportedKeywords()
    {
        // Loading via Validate() triggers the keyword scan at first call.
        // An unsupported keyword would throw NotSupportedException here.
        var result = SchemaValidator.Validate("{}", Schema.WorldDefinition);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("schemaVersion"));
    }

    [Fact]
    public void MinimalValidDocument_ValidatesClean()
    {
        var result = SchemaValidator.Validate(MinimalValid, Schema.WorldDefinition);
        Assert.True(result.IsValid,
            "Minimal valid document should pass.\n" + string.Join("\n", result.Errors));
    }

    [Fact]
    public void SchemaVersions_Constant_IsCorrect()
    {
        Assert.Equal("0.1.0", SchemaVersions.WorldDefinition);
    }

    [Fact]
    public void Schema_RejectsWrongSchemaVersion()
    {
        const string json = """
            {
              "schemaVersion": "9.9.9",
              "worldId": "test", "name": "Test", "seed": 1,
              "floors": [], "rooms": [], "lightSources": [],
              "lightApertures": [], "npcSlots": [], "objectsAtAnchors": []
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldDefinition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("schemaVersion") || e.Contains("9.9.9"));
    }

    // ── AT-02: starter file validates clean ──────────────────────────────────

    [Fact]
    public void StarterFile_ValidatesClean()
    {
        var json   = LoadStarterJson();
        var result = SchemaValidator.Validate(json, Schema.WorldDefinition);
        Assert.True(result.IsValid,
            "office-starter.json failed schema validation:\n" +
            string.Join("\n", result.Errors));
    }

    // ── AT-06: malformed files are rejected with specific path in error ──────

    [Fact]
    public void MissingSchemaVersion_RejectsWithPath()
    {
        const string json = """
            {
              "worldId": "test", "name": "Test", "seed": 1,
              "floors": [], "rooms": [], "lightSources": [],
              "lightApertures": [], "npcSlots": [], "objectsAtAnchors": []
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldDefinition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("schemaVersion"));
    }

    [Fact]
    public void NegativeSeed_IsRejected()
    {
        const string json = """
            {
              "schemaVersion": "0.1.0", "worldId": "test", "name": "Test", "seed": -1,
              "floors": [], "rooms": [], "lightSources": [],
              "lightApertures": [], "npcSlots": [], "objectsAtAnchors": []
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldDefinition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("seed") || e.Contains("minimum"));
    }

    [Fact]
    public void UnknownTopLevelProperty_IsRejected()
    {
        const string json = """
            {
              "schemaVersion": "0.1.0", "worldId": "test", "name": "Test", "seed": 1,
              "floors": [], "rooms": [], "lightSources": [],
              "lightApertures": [], "npcSlots": [], "objectsAtAnchors": [],
              "unknownField": "bad"
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldDefinition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unknownField"));
    }

    [Fact]
    public void RoomWithZeroWidthBounds_IsRejected()
    {
        const string json = """
            {
              "schemaVersion": "0.1.0", "worldId": "t", "name": "T", "seed": 1,
              "floors": [{ "id": "f1", "name": "First", "floorEnum": "first" }],
              "rooms": [{
                "id": "r1", "name": "Room", "category": "breakroom", "floorId": "f1",
                "bounds": { "x": 0, "y": 0, "width": 0, "height": 10 },
                "initialIllumination": { "ambientLevel": 50, "colorTemperatureK": 4000 }
              }],
              "lightSources": [], "lightApertures": [], "npcSlots": [], "objectsAtAnchors": []
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldDefinition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("width") || e.Contains("minimum"));
    }

    [Fact]
    public void LightSourceWithColorTempBelowMinimum_IsRejected()
    {
        const string json = """
            {
              "schemaVersion": "0.1.0", "worldId": "t", "name": "T", "seed": 1,
              "floors": [], "rooms": [],
              "lightSources": [{
                "id": "ls1", "kind": "overheadFluorescent", "state": "on",
                "intensity": 50, "colorTemperatureK": 50,
                "position": { "x": 5, "y": 5 }, "roomId": "r1"
              }],
              "lightApertures": [], "npcSlots": [], "objectsAtAnchors": []
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldDefinition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("colorTemperatureK") || e.Contains("minimum"));
    }

    // ── AT-07: free archetypeHint validates clean ─────────────────────────────

    [Fact]
    public void NpcSlot_WithUnknownArchetypeHint_ValidatesClean()
    {
        const string json = """
            {
              "schemaVersion": "0.1.0", "worldId": "t", "name": "T", "seed": 1,
              "floors": [], "rooms": [], "lightSources": [], "lightApertures": [],
              "npcSlots": [{ "id": "slot-1", "archetypeHint": "not-a-real-archetype" }],
              "objectsAtAnchors": []
            }
            """;

        var result = SchemaValidator.Validate(json, Schema.WorldDefinition);

        Assert.True(result.IsValid,
            "A free archetypeHint should validate clean — schema does not constrain hint values.\n" +
            string.Join("\n", result.Errors));
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static string LoadStarterJson()
    {
        // Walk up from the test working directory until we find the repo root.
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(
                dir.FullName, "docs", "c2-content", "world-definitions", "office-starter.json");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate office-starter.json. Run tests from the solution root.");
    }
}
