using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warden.Contracts.Handshake;

/// <summary>
/// Up to 25 deterministic simulation scenarios produced by one Sonnet for
/// Tier-3 validation. Submitted as a single Message Batches API call.
/// Schema: <c>sonnet-to-haiku.schema.json</c> v0.1.0.
/// </summary>
public sealed record ScenarioBatch
{
    public string              SchemaVersion { get; init; } = "0.1.0";
    public string              BatchId       { get; init; } = string.Empty;
    public string              ParentSpecId  { get; init; } = string.Empty;
    public List<ScenarioDto>   Scenarios     { get; init; } = new();

    // Optional
    public SharedContext? SharedContext { get; init; }
}

public sealed record SharedContext
{
    public string? Narrative     { get; init; }
    public string? SuccessMetric { get; init; }
}

public sealed record ScenarioDto
{
    public string              ScenarioId           { get; init; } = string.Empty;
    public int                 Seed                 { get; init; }
    public double              DurationGameSeconds  { get; init; }
    public List<ScenarioAssertionDto> Assertions    { get; init; } = new();

    // Optional
    public ConfigDelta?              ConfigDelta { get; init; }
    public List<ScenarioCommandDto>? Commands    { get; init; }
}

public sealed record ConfigDelta
{
    public string?      Path  { get; init; }
    public JsonElement? Value { get; init; }
}

public sealed record ScenarioCommandDto
{
    public ScenarioCommandKind           Kind           { get; init; }
    public double                        AtGameSeconds  { get; init; }
    public Dictionary<string, JsonElement>? Payload    { get; init; }
}

public sealed record ScenarioAssertionDto
{
    public string          Id     { get; init; } = string.Empty;
    public AssertionKind   Kind   { get; init; }
    public string          Target { get; init; } = string.Empty;

    // Optional
    public string?      Op                  { get; init; }
    public JsonElement? Value               { get; init; }
    public JsonElement? Value2              { get; init; }
    public double?      WithinGameSeconds   { get; init; }
}

// -- Enums ---------------------------------------------------------------------

/// <summary>Assertion evaluation timing. Serialises as kebab-case string.</summary>
[JsonConverter(typeof(JsonKebabCaseEnumConverter<AssertionKind>))]
public enum AssertionKind { Eventual, Never, AtEnd, Count }

/// <summary>Scenario command type. Serialises as kebab-case string.</summary>
[JsonConverter(typeof(JsonKebabCaseEnumConverter<ScenarioCommandKind>))]
public enum ScenarioCommandKind
{
    SpawnFood,
    SpawnLiquid,
    RemoveEntity,
    SetPosition,
    ForceDominant
}
