using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warden.Contracts.Handshake;

/// <summary>
/// Single engineering objective handed from Tier-1 (Opus) to Tier-2 (Sonnet).
/// A mission produces 1–5 of these. Each is independently actionable.
/// Schema: <c>opus-to-sonnet.schema.json</c> v0.1.0.
/// </summary>
public sealed record OpusSpecPacket
{
    public string          SchemaVersion    { get; init; } = "0.1.0";
    public string          SpecId           { get; init; } = string.Empty;
    public string          MissionId        { get; init; } = string.Empty;
    public string          Title            { get; init; } = string.Empty;
    public string          Rationale        { get; init; } = string.Empty;
    public SpecInputs      Inputs           { get; init; } = default!;
    public List<SpecDeliverable>     Deliverables     { get; init; } = new();
    public List<SpecAcceptanceTest>  AcceptanceTests  { get; init; } = new();
    public int             TimeboxMinutes   { get; init; }
    public double          WorkerBudgetUsd  { get; init; }

    // Optional
    public List<string>?           NonGoals             { get; init; }
    public bool?                   NeedsHaikuValidation { get; init; }
    public bool                    SpatialContext        { get; init; } = false;
}

public sealed record SpecInputs
{
    public List<string>                     ReferenceFiles { get; init; } = new();
    public List<string>?                    Preconditions  { get; init; }
    public Dictionary<string, JsonElement>? Constants      { get; init; }
}

public sealed record SpecDeliverable
{
    public DeliverableKind Kind        { get; init; }
    public string          Path        { get; init; } = string.Empty;
    public string          Description { get; init; } = string.Empty;
}

public sealed record SpecAcceptanceTest
{
    public string           Id           { get; init; } = string.Empty;
    public string           Assertion    { get; init; } = string.Empty;
    public VerificationKind Verification { get; init; }
    public string?          Notes        { get; init; }
}

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>Deliverable kind. Serialises as camelCase lowercase string.</summary>
public enum DeliverableKind { Code, Test, Doc, Schema, Config }

/// <summary>Verification method. Serialises as kebab-case string.</summary>
[JsonConverter(typeof(JsonKebabCaseEnumConverter<VerificationKind>))]
public enum VerificationKind
{
    UnitTest,
    Build,
    ManualReview,
    SchemaValidation,
    CliExitCode
}
