using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Warden.Contracts.Telemetry;

// ── Root ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Versioned, AI-consumable projection of <c>SimulationSnapshot</c>.
/// Produced by <c>Warden.Telemetry.TelemetryProjector</c> (WP-03), consumed by
/// Tier-3 Haikus. Schema: <c>world-state.schema.json</c> v0.3.0.
/// </summary>
public sealed record WorldStateDto
{
    public string                      SchemaVersion { get; init; } = "0.3.0";
    public DateTimeOffset              CapturedAt    { get; init; }
    public int                         Tick          { get; init; }
    public ClockStateDto               Clock         { get; init; } = default!;
    public List<EntityStateDto>        Entities      { get; init; } = new();
    public List<WorldItemDto>          WorldItems    { get; init; } = new();
    public List<WorldObjectDto>        WorldObjects  { get; init; } = new();
    public InvariantDigestDto          Invariants    { get; init; } = default!;

    // Optional core fields
    public int?                    Seed         { get; init; }
    public string?                 SimVersion   { get; init; }
    public List<TransitItemDto>?   TransitItems { get; init; }

    // v0.2 — social pillar (optional; projector omits until populated)
    public List<RelationshipDto>?  Relationships { get; init; }
    public List<MemoryEventDto>?   MemoryEvents  { get; init; }

    // v0.3 — spatial pillar (optional; projector omits until populated)
    public IReadOnlyList<RoomDto>?         Rooms          { get; init; }
    public IReadOnlyList<LightSourceDto>?  LightSources   { get; init; }
    public IReadOnlyList<LightApertureDto>? LightApertures { get; init; }
}

// ── Clock ─────────────────────────────────────────────────────────────────────

public sealed record ClockStateDto
{
    public string      GameTimeDisplay { get; init; } = string.Empty;
    public int         DayNumber       { get; init; }
    public bool        IsDaytime       { get; init; }
    public float       CircadianFactor { get; init; }
    public float       TimeScale       { get; init; }

    // v0.3 — optional sun position
    public SunStateDto? Sun            { get; init; }
}

// ── Entity ────────────────────────────────────────────────────────────────────

public sealed record EntityStateDto
{
    public string             Id         { get; init; } = string.Empty;
    public string             ShortId    { get; init; } = string.Empty;
    public string             Name       { get; init; } = string.Empty;
    public SpeciesType        Species    { get; init; }
    public PositionStateDto   Position   { get; init; } = default!;
    public DrivesStateDto     Drives     { get; init; } = default!;
    public PhysiologyStateDto Physiology { get; init; } = default!;

    // v0.2 — optional social state
    public SocialStateDto?    Social     { get; init; }
}

public sealed record PositionStateDto
{
    public float  X           { get; init; }
    public float  Y           { get; init; }
    public float  Z           { get; init; }
    public bool   HasPosition { get; init; }

    // Optional
    public bool?   IsMoving   { get; init; }
    public string? MoveTarget { get; init; }
}

public sealed record DrivesStateDto
{
    public DominantDrive Dominant        { get; init; }
    public float         EatUrgency      { get; init; }
    public float         DrinkUrgency    { get; init; }
    public float         SleepUrgency    { get; init; }
    public float         DefecateUrgency { get; init; }
    public float         PeeUrgency      { get; init; }
}

public sealed record PhysiologyStateDto
{
    public float Satiation  { get; init; }
    public float Hydration  { get; init; }
    public float BodyTemp   { get; init; }
    public float Energy     { get; init; }
    public float Sleepiness { get; init; }
    public bool  IsSleeping { get; init; }

    // Optional
    public float? SiFill      { get; init; }
    public float? LiFill      { get; init; }
    public float? ColonFill   { get; init; }
    public float? BladderFill { get; init; }
}

// ── World items ───────────────────────────────────────────────────────────────

public sealed record WorldItemDto
{
    public string Id    { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;

    // Optional
    public float? RotLevel { get; init; }
    public bool?  IsRotten { get; init; }
}

public sealed record WorldObjectDto
{
    public string          Id         { get; init; } = string.Empty;
    public string          Name       { get; init; } = string.Empty;
    public WorldObjectKind Kind       { get; init; }
    public float           X          { get; init; }
    public float           Y          { get; init; }
    public float           Z          { get; init; }

    // Optional
    public int? StockCount { get; init; }
}

public sealed record TransitItemDto
{
    public string Id             { get; init; } = string.Empty;
    public string TargetEntityId { get; init; } = string.Empty;
    public string ContentLabel   { get; init; } = string.Empty;
    public float  Progress       { get; init; }
}

// ── Invariants ────────────────────────────────────────────────────────────────

public sealed record InvariantDigestDto
{
    public int                    ViolationCount    { get; init; }
    public List<InvariantEventDto>? RecentViolations { get; init; }
}

public sealed record InvariantEventDto
{
    public int    Tick    { get; init; }
    public string Kind    { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>Entity species. Serialises as camelCase lowercase string.</summary>
public enum SpeciesType { Human, Cat, Unknown }

/// <summary>
/// Active dominant drive. Serialises without a naming policy so values
/// match the schema's PascalCase enum: "None", "Eat", "Drink", etc.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DominantDrive { None, Eat, Drink, Sleep, Defecate, Pee }

/// <summary>World-object type. Serialises as camelCase lowercase string.</summary>
public enum WorldObjectKind { Fridge, Sink, Toilet, Bed, Other }
