namespace APIFramework.Systems;

/// <summary>Kinds of willpower deltas that can be enqueued on <see cref="WillpowerEventQueue"/>.</summary>
public enum WillpowerEventKind
{
    /// <summary>Willpower cost — magnitude is subtracted from Current.</summary>
    SuppressionTick,  // willpower cost (magnitude subtracted from Current)
    /// <summary>Willpower recovery — magnitude is added to Current.</summary>
    RestTick          // willpower recovery (magnitude added to Current)
}

/// <summary>
/// Signal pushed into <see cref="WillpowerEventQueue"/> by any system that causes
/// willpower change. <see cref="WillpowerSystem"/> drains the queue each tick and
/// applies deltas. <paramref name="EntityId"/> is the lower 32 bits of the entity's
/// deterministic Guid counter.
/// </summary>
/// <param name="EntityId">Low 32 bits of the target entity's Guid (see <see cref="WillpowerSystem.EntityIntId"/>).</param>
/// <param name="Kind">Whether this is a willpower cost or recovery.</param>
/// <param name="Magnitude">0–10 cost or recovery applied to <see cref="WillpowerComponent"/>.Current.</param>
public readonly record struct WillpowerEventSignal(
    int EntityId,
    WillpowerEventKind Kind,
    int Magnitude    // 0–10 cost or recovery per tick
);
