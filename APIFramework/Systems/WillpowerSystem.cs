using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Drains WillpowerEventQueue each tick and applies deltas to WillpowerComponent.Current.
/// Also pushes one RestTick per tick for every NPC that is currently sleeping (SleepingTag).
///
/// The system does NOT produce SuppressionTick events — those come from action-selection
/// and StressSystem (later packets). This is the clean seam: any producer enqueues a signal;
/// this system applies it.
///
/// Phase: Cognition.
/// </summary>
/// <remarks>
/// Reads: <see cref="NpcTag"/>, <see cref="WillpowerComponent"/>, <see cref="SleepingTag"/>,
/// and any <see cref="WillpowerEventSignal"/> produced this tick by upstream systems.
/// Writes: <see cref="WillpowerComponent"/> (single writer of <c>Current</c> via the queue);
/// also enqueues <see cref="WillpowerEventKind.RestTick"/> signals for sleeping NPCs.
/// Ordering: must run after <see cref="ActionSelectionSystem"/> (which enqueues
/// <see cref="WillpowerEventKind.SuppressionTick"/>) so its signals are drained the same tick.
/// </remarks>
/// <seealso cref="WillpowerEventQueue"/>
/// <seealso cref="WillpowerEventSignal"/>
/// <seealso cref="ActionSelectionSystem"/>
public class WillpowerSystem : ISystem
{
    private readonly SocialSystemConfig  _cfg;
    private readonly WillpowerEventQueue _queue;

    /// <summary>
    /// Constructs the system.
    /// </summary>
    /// <param name="cfg">Social-system tuning; supplies <c>WillpowerSleepRegenPerTick</c>.</param>
    /// <param name="queue">Shared event queue. The system drains this queue each tick;
    /// upstream producers (e.g. <see cref="ActionSelectionSystem"/>) enqueue suppression signals.</param>
    public WillpowerSystem(SocialSystemConfig cfg, WillpowerEventQueue queue)
    {
        _cfg   = cfg;
        _queue = queue;
    }

    /// <summary>
    /// Per-tick update. Pushes <see cref="WillpowerEventKind.RestTick"/> signals for every
    /// alive sleeping NPC, then drains the queue and applies each signal's delta to the
    /// target NPC's <see cref="WillpowerComponent.Current"/>, clamped to 0–100.
    /// </summary>
    public void Update(EntityManager em, float deltaTime)
    {
        // Push RestTick signals for sleeping NPCs
        foreach (var entity in em.Query<NpcTag>().ToList())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;
            if (!entity.Has<WillpowerComponent>()) continue;
            if (!entity.Has<SleepingTag>()) continue;

            _queue.Enqueue(new WillpowerEventSignal(
                EntityIntId(entity),
                WillpowerEventKind.RestTick,
                _cfg.WillpowerSleepRegenPerTick));
        }

        // Build a lookup so signals can find their entity in O(1)
        var map = new Dictionary<int, Entity>();
        foreach (var entity in em.Query<NpcTag>())
            map[EntityIntId(entity)] = entity;

        // Drain queue and apply
        foreach (var signal in _queue.DrainAll())
        {
            if (!map.TryGetValue(signal.EntityId, out var entity)) continue;
            if (!entity.Has<WillpowerComponent>()) continue;

            var wp = entity.Get<WillpowerComponent>();

            if (signal.Kind == WillpowerEventKind.RestTick)
                wp.Current = Math.Clamp(wp.Current + signal.Magnitude, 0, 100);
            else
                wp.Current = Math.Clamp(wp.Current - signal.Magnitude, 0, 100);

            entity.Add(wp);
        }
    }

    /// <summary>
    /// Extracts the low-32-bit entity counter from the deterministic Guid created by EntityManager.
    /// EntityManager stores the counter in bytes 0–7 of the Guid (little-endian).
    /// </summary>
    public static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }
}
