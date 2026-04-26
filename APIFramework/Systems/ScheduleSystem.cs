using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Phase: Condition (20). Runs before ActionSelectionSystem (Cognition 30).
/// For each NPC with ScheduleComponent, resolves the active schedule block from the current
/// game hour and writes CurrentScheduleBlockComponent so ActionSelectionSystem can read it.
/// </summary>
public sealed class ScheduleSystem : ISystem
{
    private readonly SimulationClock _clock;

    public ScheduleSystem(SimulationClock clock)
    {
        _clock = clock;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        float gameHour = _clock.GameHour;

        // Build anchor tag → entity Guid map once per tick.
        var anchorMap = BuildAnchorMap(em);

        foreach (var npc in em.Query<NpcTag>()
                              .Where(e => e.Has<ScheduleComponent>())
                              .ToList())
        {
            var schedule = npc.Get<ScheduleComponent>();
            var prev     = npc.Has<CurrentScheduleBlockComponent>()
                ? npc.Get<CurrentScheduleBlockComponent>()
                : new CurrentScheduleBlockComponent { ActiveBlockIndex = -1, AnchorEntityId = Guid.Empty };

            int activeIdx = FindActiveBlock(schedule.Blocks, gameHour);

            Guid anchorGuid = Guid.Empty;
            ScheduleActivityKind activity = prev.Activity;

            if (activeIdx >= 0)
            {
                var block = schedule.Blocks[activeIdx];
                activity = block.Activity;

                // Re-resolve anchor only when block index changes.
                if (activeIdx != prev.ActiveBlockIndex)
                    anchorMap.TryGetValue(block.AnchorId, out anchorGuid);
                else
                    anchorGuid = prev.AnchorEntityId;
            }

            npc.Add(new CurrentScheduleBlockComponent
            {
                ActiveBlockIndex = activeIdx,
                AnchorEntityId   = anchorGuid,
                Activity         = activity
            });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int FindActiveBlock(IReadOnlyList<ScheduleBlock> blocks, float gameHour)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            var b = blocks[i];
            if (IsBlockActive(b.StartHour, b.EndHour, gameHour))
                return i;
        }
        return -1;
    }

    internal static bool IsBlockActive(float startHour, float endHour, float gameHour)
    {
        if (endHour > startHour)
            return gameHour >= startHour && gameHour < endHour;
        // Wrap-around: e.g. 17:00 → 06:00 spans midnight.
        return gameHour >= startHour || gameHour < endHour;
    }

    private static Dictionary<string, Guid> BuildAnchorMap(EntityManager em)
    {
        var map = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var e in em.GetAllEntities())
        {
            if (!e.Has<NamedAnchorComponent>()) continue;
            var anchor = e.Get<NamedAnchorComponent>();
            if (!map.ContainsKey(anchor.Tag))
                map[anchor.Tag] = e.Id;
        }
        return map;
    }
}
