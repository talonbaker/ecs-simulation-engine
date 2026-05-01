using System;

namespace APIFramework.Components;

public enum ChoreKind
{
    CleanMicrowave    = 0,
    CleanFridge       = 1,
    CleanBathroom     = 2,
    TakeOutTrash      = 3,
    RefillWaterCooler = 4,
    RestockSupplyCloset = 5,
    ReplaceToner      = 6,
}

/// <summary>
/// One recurring office chore entity. Tracks completion state, quality, scheduling,
/// and the current assignee. CompletionLevel below 0.5 means the chore is "dirty."
/// </summary>
public struct ChoreComponent
{
    public ChoreKind Kind;
    public float     CompletionLevel;         // 0..1; below 0.5 = "dirty"
    public float     QualityOfLastExecution;  // 0..1
    public long      LastDoneTick;
    public long      NextScheduledTick;
    public Guid      CurrentAssigneeId;       // Guid.Empty = unassigned
    public Guid      TargetAnchorId;          // microwave entity id, etc.
}
