using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using APIFramework.Components;
using APIFramework.Core;
using Newtonsoft.Json;

namespace APIFramework.Systems;

/// <summary>
/// PreUpdate phase. For each NPC with <see cref="NpcArchetypeComponent"/> and no
/// <see cref="ScheduleComponent"/>, attaches the schedule loaded from
/// <c>archetype-schedules.json</c> plus a fresh <see cref="CurrentScheduleBlockComponent"/>.
/// Idempotent: existing schedules are never overwritten.
/// </summary>
/// <remarks>
/// Reads: <see cref="NpcTag"/>, <see cref="NpcArchetypeComponent"/>.<br/>
/// Writes: <see cref="ScheduleComponent"/> and
/// <see cref="CurrentScheduleBlockComponent"/> (single writer for both at attach time).<br/>
/// Phase: PreUpdate.
/// </remarks>
public sealed class ScheduleSpawnerSystem : ISystem
{
    private readonly string _schedulesPath;
    private Dictionary<string, IReadOnlyList<ScheduleBlock>>? _schedulesByArchetype;

    /// <summary>Default schedule-file location searched upward from the current working directory.</summary>
    public const string DefaultFileName = "docs/c2-content/schedules/archetype-schedules.json";

    /// <summary>Constructs the schedule-spawner system.</summary>
    /// <param name="schedulesPath">Optional explicit path to the schedule file; when null, the default location is searched upward from the CWD.</param>
    public ScheduleSpawnerSystem(string? schedulesPath = null)
    {
        _schedulesPath = schedulesPath ?? FindSchedulesFile() ?? "";
    }

    /// <summary>Per-tick spawner pass; lazily loads schedules on first run, then attaches missing schedules to NPCs.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds, unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        if (_schedulesByArchetype == null)
            _schedulesByArchetype = LoadSchedules(_schedulesPath);

        foreach (var npc in em.Query<NpcTag>()
                              .Where(e => e.Has<NpcArchetypeComponent>() && !e.Has<ScheduleComponent>())
                              .OrderBy(WillpowerSystem.EntityIntId)  // deterministic order
                              .ToList())
        {
            var archetype = npc.Get<NpcArchetypeComponent>();
            if (_schedulesByArchetype.TryGetValue(archetype.ArchetypeId, out var blocks))
            {
                npc.Add(new ScheduleComponent { Blocks = blocks });
                npc.Add(new CurrentScheduleBlockComponent
                {
                    ActiveBlockIndex = -1,
                    AnchorEntityId   = Guid.Empty
                });
            }
        }
    }

    // -- Loading ---------------------------------------------------------------

    internal static Dictionary<string, IReadOnlyList<ScheduleBlock>> LoadSchedules(string path)
    {
        var result = new Dictionary<string, IReadOnlyList<ScheduleBlock>>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Console.WriteLine($"[ScheduleSpawner] Schedule file '{path}' not found — no schedules loaded.");
            return result;
        }

        var json = File.ReadAllText(path);
        var root = JsonConvert.DeserializeObject<ScheduleFileDto>(json,
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore });

        if (root?.ArchetypeSchedules == null) return result;

        foreach (var entry in root.ArchetypeSchedules)
        {
            if (entry.ArchetypeId == null || entry.Blocks == null) continue;

            var blocks = entry.Blocks
                .Select(b => new ScheduleBlock(
                    b.StartHour,
                    b.EndHour,
                    b.AnchorId ?? "",
                    ParseActivity(b.Activity)))
                .ToList();

            result[entry.ArchetypeId] = blocks;
        }

        return result;
    }

    private static ScheduleActivityKind ParseActivity(string? activity) =>
        Enum.TryParse<ScheduleActivityKind>(activity, ignoreCase: true, out var kind)
            ? kind
            : ScheduleActivityKind.AtDesk;

    internal static string? FindSchedulesFile()
    {
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, DefaultFileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        return null;
    }

    // -- DTOs ------------------------------------------------------------------

    private class ScheduleFileDto
    {
        public string?                SchemaVersion     { get; set; }
        public List<ArchetypeEntryDto>? ArchetypeSchedules { get; set; }
    }

    private class ArchetypeEntryDto
    {
        public string?           ArchetypeId { get; set; }
        public List<BlockDto>?   Blocks      { get; set; }
    }

    private class BlockDto
    {
        public float   StartHour  { get; set; }
        public float   EndHour    { get; set; }
        public string? AnchorId   { get; set; }
        public string? Activity   { get; set; }
    }
}
