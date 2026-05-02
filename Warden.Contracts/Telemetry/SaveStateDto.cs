using System.Collections.Generic;

namespace Warden.Contracts.Telemetry;

// ── Enums (mirrored from APIFramework to keep Contracts dependency-free) ─────

public enum SaveLifeState  { Alive = 0, Incapacitated = 1, Deceased = 2 }
public enum SaveCauseOfDeath { Unknown = 0, Choked = 1, SlippedAndFell = 2, StarvedAlone = 3 }
public enum SaveScheduleActivity { AtDesk, Break, Meeting, Lunch, Outside, Roaming, Sleeping }

// ── NPC entity ────────────────────────────────────────────────────────────────

/// <summary>Complete persistent snapshot of one NPC (or corpse) entity.</summary>
public sealed record NpcSaveDto
{
    public string Id   { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool   IsHuman { get; init; } = true;

    // Position
    public float PosX { get; init; }
    public float PosY { get; init; }
    public float PosZ { get; init; }

    // MetabolismComponent
    public float Satiation         { get; init; }
    public float Hydration         { get; init; }
    public float BodyTemp          { get; init; }
    public float SatiationDrainRate { get; init; }
    public float HydrationDrainRate { get; init; }

    // EnergyComponent
    public float Energy    { get; init; }
    public float Sleepiness { get; init; }
    public bool  IsSleeping { get; init; }

    // GI fill volumes (rates restored from config)
    public float StomachVolumeMl    { get; init; }
    public float SiChymeVolumeMl    { get; init; }
    public float LiContentVolumeMl  { get; init; }
    public float ColonStoolVolumeMl { get; init; }
    public float BladderVolumeMl    { get; init; }

    // Optional components (null = component not present on entity)
    public LifeStateSaveDto?    LifeState    { get; init; }
    public ChokeSaveDto?        Choking      { get; init; }
    public FaintSaveDto?        Fainting     { get; init; }
    public LockedInSaveDto?     LockedIn     { get; init; }
    public CauseOfDeathSaveDto? CauseOfDeath { get; init; }
    public CorpseSaveDto?       Corpse       { get; init; }
    public StressSaveDto?       Stress       { get; init; }
    public MaskSaveDto?         Mask         { get; init; }
    public MoodSaveDto?         Mood         { get; init; }
    public WillpowerSaveDto?    Willpower    { get; init; }
    public WorkloadSaveDto?     Workload     { get; init; }

    public IReadOnlyList<ScheduleBlockSaveDto>? ScheduleBlocks      { get; init; }
    public IReadOnlyList<string>?               EncounteredCorpseIds { get; init; }
    public IReadOnlyList<string>?               Tags                 { get; init; }
}

public sealed record LifeStateSaveDto
{
    public SaveLifeState State                  { get; init; }
    public long          LastTransitionTick      { get; init; }
    public int           IncapacitatedTickBudget { get; init; }
    public SaveCauseOfDeath PendingDeathCause   { get; init; }
}

public sealed record ChokeSaveDto
{
    public long          ChokeStartTick { get; init; }
    public int           RemainingTicks { get; init; }
    public float         BolusSize      { get; init; }
    public SaveCauseOfDeath PendingCause { get; init; }
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
    public SaveCauseOfDeath Cause           { get; init; }
    public long             DeathTick       { get; init; }
    public string           WitnessedById   { get; init; } = string.Empty;
    public string           LocationRoomId  { get; init; } = string.Empty;
}

public sealed record CorpseSaveDto
{
    public long   DeathTick            { get; init; }
    public string OriginalNpcEntityId  { get; init; } = string.Empty;
    public string? LocationRoomId      { get; init; }
    public bool   HasBeenMoved         { get; init; }
}

public sealed record StressSaveDto
{
    public int    AcuteLevel                { get; init; }
    public double ChronicLevel              { get; init; }
    public int    LastDayUpdated            { get; init; }
    public int    SuppressionEventsToday    { get; init; }
    public int    DriveSpikeEventsToday     { get; init; }
    public int    SocialConflictEventsToday { get; init; }
    public int    OverdueTaskEventsToday    { get; init; }
    public int    BurnoutLastAppliedDay     { get; init; }
    public int    WitnessedDeathEventsToday { get; init; }
    public int    BereavementEventsToday    { get; init; }
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

public sealed record WorkloadSaveDto
{
    public int                     Capacity    { get; init; }
    public int                     CurrentLoad { get; init; }
    public IReadOnlyList<string>?  ActiveTaskIds { get; init; }
}

public sealed record ScheduleBlockSaveDto
{
    public float                StartHour { get; init; }
    public float                EndHour   { get; init; }
    public string               AnchorId  { get; init; } = string.Empty;
    public SaveScheduleActivity Activity  { get; init; }
}

// ── Task entity ───────────────────────────────────────────────────────────────

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

// ── Stain entity ──────────────────────────────────────────────────────────────

public sealed record StainEntitySaveDto
{
    public string Id              { get; init; } = string.Empty;
    public float  PosX            { get; init; }
    public float  PosZ            { get; init; }
    public string Source          { get; init; } = string.Empty;
    public int    Magnitude       { get; init; }
    public long   CreatedAtTick   { get; init; }
    public string ChronicleEntryId { get; init; } = string.Empty;

    // Optional
    public float? FallRisk   { get; init; }
    public bool   IsObstacle { get; init; }
}

// ── Locked-door entity ────────────────────────────────────────────────────────

public sealed record LockedDoorSaveDto
{
    public string  Id   { get; init; } = string.Empty;
    public float   PosX { get; init; }
    public float   PosY { get; init; }
    public float   PosZ { get; init; }
    public string? Name { get; init; }
}
