namespace Warden.Contracts.SchemaValidation;

/// <summary>
/// Names each JSON Schema embedded in <c>Warden.Contracts</c>.
/// Pass to <see cref="SchemaValidator.Validate"/> to select which schema
/// to validate against.
///
/// Resource names follow the pattern:
///   <c>Warden.Contracts.SchemaValidation.&lt;file&gt;.schema.json</c>
/// </summary>
public enum Schema
{
    /// <summary><c>world-state.schema.json</c> — <c>WorldStateDto</c></summary>
    WorldState,

    /// <summary><c>opus-to-sonnet.schema.json</c> — <c>OpusSpecPacket</c></summary>
    OpusToSonnet,

    /// <summary><c>sonnet-result.schema.json</c> — <c>SonnetResult</c></summary>
    SonnetResult,

    /// <summary><c>sonnet-to-haiku.schema.json</c> — <c>ScenarioBatch</c></summary>
    SonnetToHaiku,

    /// <summary><c>haiku-result.schema.json</c> — <c>HaikuResult</c></summary>
    HaikuResult,

    /// <summary><c>ai-command-batch.schema.json</c> — <c>AiCommandBatch</c></summary>
    AiCommandBatch,

    /// <summary><c>world-definition.schema.json</c> — <c>WorldDefinitionDto</c></summary>
    WorldDefinition
}

/// <summary>
/// Current schema version constants. Bump the relevant entry when a packet
/// lands a minor version on that schema; leave others unchanged.
/// </summary>
public static class SchemaVersions
{
    public const string WorldState    = "0.4.0";
    public const string OpusToSonnet  = "0.1.0";
    public const string SonnetResult  = "0.1.0";
    public const string SonnetToHaiku = "0.1.0";
    public const string HaikuResult   = "0.1.0";
    public const string AiCommandBatch  = "0.1.0";
    public const string WorldDefinition = "0.1.0";
}
