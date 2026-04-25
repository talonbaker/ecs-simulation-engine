namespace APIFramework.Systems;

public enum WillpowerEventKind
{
    SuppressionTick,  // willpower cost (magnitude subtracted from Current)
    RestTick          // willpower recovery (magnitude added to Current)
}

/// <summary>
/// Signal pushed into WillpowerEventQueue by any system that causes willpower change.
/// WillpowerSystem drains the queue each tick and applies deltas.
/// EntityId is the lower 32 bits of the entity's deterministic Guid counter.
/// </summary>
public readonly record struct WillpowerEventSignal(
    int EntityId,
    WillpowerEventKind Kind,
    int Magnitude    // 0–10 cost or recovery per tick
);
