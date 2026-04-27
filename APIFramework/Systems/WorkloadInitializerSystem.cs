using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

// ── Deserialization DTOs ──────────────────────────────────────────────────────

internal sealed class WorkloadCapacityDto
{
    public string SchemaVersion { get; set; } = "";
    public WorkloadCapacityEntryDto[] ArchetypeCapacity { get; set; } = Array.Empty<WorkloadCapacityEntryDto>();
}

internal sealed class WorkloadCapacityEntryDto
{
    public string ArchetypeId { get; set; } = "";
    public int    Capacity    { get; set; } = 3;
}

// ── System ────────────────────────────────────────────────────────────────────

/// <summary>
/// Attaches <see cref="WorkloadComponent"/> to every NPC that has
/// <see cref="NpcArchetypeComponent"/> but no workload state yet.
/// Capacity is loaded from archetype-workload-capacity.json; defaults to 3 when unknown.
/// Phase: PreUpdate — runs before any system that reads workload state.
/// Idempotent: entities that already have <see cref="WorkloadComponent"/> are skipped.
/// </summary>
public class WorkloadInitializerSystem : ISystem
{
    private readonly IReadOnlyDictionary<string, int> _capacities;

    public WorkloadInitializerSystem(IReadOnlyDictionary<string, int> capacities)
    {
        _capacities = capacities;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<NpcTag>().ToList())
        {
            if (entity.Has<WorkloadComponent>()) continue;

            int capacity = 3; // default
            if (entity.Has<NpcArchetypeComponent>())
            {
                var archetypeId = entity.Get<NpcArchetypeComponent>().ArchetypeId;
                if (_capacities.TryGetValue(archetypeId, out var v))
                    capacity = v;
            }

            entity.Add(new WorkloadComponent
            {
                ActiveTasks  = new List<Guid>(),
                Capacity     = Math.Max(1, capacity),
                CurrentLoad  = 0
            });
        }
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the archetype workload capacities from the given JSON path,
    /// or searches upward from the CWD for the default file.
    /// Returns a default-capacity dictionary when the file cannot be found or parsed.
    /// </summary>
    public static IReadOnlyDictionary<string, int> LoadCapacities(string? path = null)
    {
        path ??= FindDefaultPath();
        if (path is null || !File.Exists(path))
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto  = JsonSerializer.Deserialize<WorkloadCapacityDto>(json, opts);
            if (dto?.ArchetypeCapacity is null)
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in dto.ArchetypeCapacity)
                result[entry.ArchetypeId] = Math.Clamp(entry.Capacity, 1, 10);
            return result;
        }
        catch
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Walks up from the CWD to locate the default capacities file.</summary>
    public static string? FindDefaultPath()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 8; i++)
        {
            if (dir is null) break;
            var candidate = Path.Combine(
                dir.FullName, "docs", "c2-content", "archetypes",
                "archetype-workload-capacity.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
