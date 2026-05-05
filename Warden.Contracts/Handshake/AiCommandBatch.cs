using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Warden.Contracts.Handshake;

/// <summary>
/// Whitelisted mutations an AI can inject into a running ECSCli via
/// <c>ECSCli ai inject --in commands.json</c>. Anything outside the union of
/// known command types is rejected with exit code 3.
/// Schema: <c>ai-command-batch.schema.json</c> v0.1.0.
/// </summary>
public sealed record AiCommandBatch
{
    public string           SchemaVersion { get; init; } = "0.1.0";
    public List<AiCommand>  Commands      { get; init; } = new();
}

/// <summary>
/// Discriminated union base. The JSON property <c>"kind"</c> is the
/// type discriminator; each derived type registers its literal value.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SpawnFoodCommand),    "spawn-food")]
[JsonDerivedType(typeof(SpawnLiquidCommand),  "spawn-liquid")]
[JsonDerivedType(typeof(RemoveEntityCommand), "remove-entity")]
[JsonDerivedType(typeof(SetPositionCommand),  "set-position")]
[JsonDerivedType(typeof(ForceDominantCommand),"force-dominant")]
[JsonDerivedType(typeof(SetConfigValueCommand),"set-config-value")]
public abstract record AiCommand;

// -- Concrete command types ----------------------------------------------------

public sealed record SpawnFoodCommand(
    string FoodType,
    float  X,
    float  Y,
    float  Z,
    int    Count = 1
) : AiCommand;

public sealed record SpawnLiquidCommand(
    string LiquidType,
    float  X,
    float  Y,
    float  Z,
    int    Count = 1
) : AiCommand;

public sealed record RemoveEntityCommand(
    string EntityId
) : AiCommand;

public sealed record SetPositionCommand(
    string EntityId,
    float  X,
    float  Y,
    float  Z
) : AiCommand;

public sealed record ForceDominantCommand(
    string EntityId,
    string Dominant,
    double DurationGameSeconds
) : AiCommand;

public sealed record SetConfigValueCommand(
    string                       Path,
    System.Text.Json.JsonElement Value
) : AiCommand;
