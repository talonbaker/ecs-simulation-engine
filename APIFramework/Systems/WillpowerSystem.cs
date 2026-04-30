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
/// Reads: <see cref="NpcTag"/>, <see cref="WillpowerComponent"/>,
/// <see cref="SleepingTag"/>, <see cref="LifeStateComponent"/>; drains
/// <see cref="WillpowerEventQueue"/>.<br/>
/// Writes: <see cref="WillpowerComponent"/>.Current (single writer); enqueues
/// per-tick RestTick signals for sleeping NPCs.<br/>
/// Phase: Cognition, after <see cref="ActionSelectionSystem"/>.
/// </remarks>
public class WillpowerSystem : ISystem
{
    private readonly SocialSystemConfig  _cfg;
    private readonly WillpowerEventQueue _queue;

    /// <summary>Constructs the willpower system.</summary>
    /// <param name="cfg">Social system tuning (sleep regen-per-tick magnitude).</param>
    /// <param name="queue">Shared willpower event queue.</param>
    public WillpowerSystem(SocialSystemConfig cfg, WillpowerEventQueue queue)
    {
        _cfg   = cfg;
        _queue = queue;
    }

    /// <summary>Per-tick pass: enqueues sleep RestTicks, then drains and applies the queue.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds, unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        // Push RestTick signals for sleeping NPCs
        foreach (var entity in em.Query<NpcTag>().ToList())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
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
