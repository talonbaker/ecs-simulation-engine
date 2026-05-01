using System;
using System.Collections.Generic;

namespace Warden.Contracts.Telemetry;

// ── NPC full save state ───────────────────────────────────────────────────────

/// <summary>Full per-NPC state for save/load round-trips (v0.5). Null fields = component absent.</summary>
public sealed record NpcSaveDto
{
    public string  Id        { get; init; } = string.Empty;
    public string  Name      { get; init; } = string.Empty;
    public float   PosX      { get; init; }
    public float   PosY      { get; init; }
    public float   PosZ      { get; init; }

    // Physiology — all persistent values
    public float   Satiation           { get; init; }
    public float   Hydration           { get; init; }
    public float   BodyTemp            { get; init; }
    public float   Energy              { get; init; }
    public float   Sleepiness          { get; init; }
    public bool    IsSleeping          { get; init; }
    public float   SatiationDrainRate  { get; init; }
    public float   HydrationDrainRate  { get; init; }
    public float   StomachVolumeMl     { get; init; }
    public float   SiChymeVolumeMl     { get; init; }
    public float   LiContentVolumeMl   { get; init; }
    public float   ColonStoolVolumeMl  { get; init; }
    public float   BladderVolumeMl     { get; init; }

    // Life state
    public LifeStateSaveDto?    LifeState    { get; init; }
    public ChokeSaveDto?        Choking      { get; init; }
    public FaintSaveDto?        Fainting     { get; init; }
    public LockedInSaveDto?     LockedIn     { get; init; }
    public CauseOfDeathSaveDto? CauseOfDeath { get; init; }
    public CorpseSaveDto?       Corpse       { get; init; }

    // Stress / mask / mood
    public StressSaveDto? Stress   { get; init; }
    public MaskSaveDto?   Mask     { get; init; }
    public MoodSaveDto?   Mood     { get; init; }

    // Social
    public WillpowerSaveDto? Willpower { get; init; }

    // Workload
    public WorkloadSaveDto? Workload { get; init; }

    // Bereavement history — entity IDs of corpses whose proximity hit has fired
    public IReadOnlyList<string>? EncounteredCorpseIds { get; init; }

    // Active structural tags on this entity (e.g. "Human", "Npc", "Corpse", "IsChoking")
    public IReadOnlyList<string>? Tags { get; init; }

    // Schedule blocks (definition is persisted; current block is recomputed each tick)
    public IReadOnlyList<ScheduleBlockSaveDto>? ScheduleBlocks { get; init; }
}

// ── Life state ───────────────────────────────────────────────────────────────

public sealed record LifeStateSaveDto
{
    public int  State                   { get; init; }  // LifeState enum int
    public long LastTransitionTick      { get; init; }
    public int  IncapacitatedTickBudget { get; init; }
    public int  PendingDeathCause       { get; init; }  // CauseOfDeath enum int
}

public sealed record ChokeSaveDto
{
    public long  ChokeStartTick { get; init; }
    public int   RemainingTicks { get; init; }
    public float BolusSize      { get; init; }
    public int   PendingCause   { get; init; }  // CauseOfDeath enum int
}

public sealed record FaintSaveDto
{
    public long FaintStartTick { get; init; }
    public long RecoveryTick   { get; init; }
}

public sealed record LockedInSaveDto
{
    public long FirstDetectedTick    { get; init; }
    public int  StarvationTickBudget { get; init; }
}

public sealed record CauseOfDeathSaveDto
{
    public int    Cause          { get; init; }  // CauseOfDeath enum int
    public long   DeathTick      { get; init; }
    public string WitnessedById  { get; init; } = string.Empty;  // Guid string
    public string LocationRoomId { get; init; } = string.Empty;  // Guid string
}

public sealed record CorpseSaveDto
{
    public long    DeathTick           { get; init; }
    public string  OriginalNpcEntityId { get; init; } = string.Empty;
    public string? LocationRoomId      { get; init; }
    public bool    HasBeenMoved        { get; init; }
}

// ── Stress / mask / mood / willpower ─────────────────────────────────────────

public sealed record StressSaveDto
{
    public int    AcuteLevel                { get; init; }
    public double ChronicLevel              { get; init; }
    public int    LastDayUpdated            { get; init; }
    public int    SuppressionEventsToday    { get; init; }
    public int    DriveSpikeEventsToday     { get; init; }
    public int    SocialConflictEventsToday { get; init; }
    public int    OverdueTaskEventsToday    { get; init; }
    public int    WitnessedDeathEventsToday { get; init; }
    public int    BereavementEventsToday    { get; init; }
    public int    BurnoutLastAppliedDay     { get; init; }
}

public sealed record MaskSaveDto
{
    public int  IrritationMask { get; init; }
    public int  AffectionMask  { get; init; }
    public int  AttractionMask { get; init; }
    public int  LonelinessMask { get; init; }
    public int  CurrentLoad    { get; init; }
    public int  Baseline       { get; init; }
    public long LastSlipTick   { get; init; }
}

public sealed record MoodSaveDto
{
    public float Joy          { get; init; }
    public float Trust        { get; init; }
    public float Fear         { get; init; }
    public float Surprise     { get; init; }
    public float Sadness      { get; init; }
    public float Disgust      { get; init; }
    public float Anger        { get; init; }
    public float Anticipation { get; init; }
    public float PanicLevel   { get; init; }
    public float GriefLevel   { get; init; }
}

public sealed record WillpowerSaveDto
{
    public int Current  { get; init; }
    public int Baseline { get; init; }
}

// ── Workload / tasks ──────────────────────────────────────────────────────────

public sealed record WorkloadSaveDto
{
    public IReadOnlyList<string> ActiveTaskIds { get; init; } = Array.Empty<string>();
    public int Capacity    { get; init; }
    public int CurrentLoad { get; init; }
}

public sealed record TaskSaveDto
{
    public string Id            { get; init; } = string.Empty;
    public float  EffortHours   { get; init; }
    public long   DeadlineTick  { get; init; }
    public int    Priority      { get; init; }
    public float  Progress      { get; init; }
    public float  QualityLevel  { get; init; }
    public string AssignedNpcId { get; init; } = string.Empty;
    public long   CreatedTick   { get; init; }
    public bool   IsOverdue     { get; init; }
}

// ── Schedule ──────────────────────────────────────────────────────────────────

public sealed record ScheduleBlockSaveDto
{
    public float  StartHour { get; init; }
    public float  EndHour   { get; init; }
    public string AnchorId  { get; init; } = string.Empty;
    public int    Activity  { get; init; }  // ScheduleActivityKind enum int
}

// ── Stain + fall risk ─────────────────────────────────────────────────────────

public sealed record StainEntitySaveDto
{
    public string  Id               { get; init; } = string.Empty;
    public float   PosX             { get; init; }
    public float   PosY             { get; init; }
    public float   PosZ             { get; init; }
    public string  Source           { get; init; } = string.Empty;
    public int     Magnitude        { get; init; }
    public long    CreatedAtTick    { get; init; }
    public string  ChronicleEntryId { get; init; } = string.Empty;
    public float?  FallRiskLevel    { get; init; }
}

// ── Locked doors (structural entities carrying LockedTag) ─────────────────────

public sealed record LockedDoorSaveDto
{
    public string  Id   { get; init; } = string.Empty;
    public float   PosX { get; init; }
    public float   PosY { get; init; }
    public float   PosZ { get; init; }
    public string? Name { get; init; }
}
