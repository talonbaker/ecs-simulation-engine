using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

// -- Deserialization DTOs ------------------------------------------------------

/// <summary>Top-level deserialization shape for archetype-workload-capacity.json.</summary>
internal sealed class WorkloadCapacityDto
{
    /// <summary>Schema version string from the source file.</summary>
    public string SchemaVersion { get; set; } = "";
    /// <summary>Per-archetype capacity entries.</summary>
    public WorkloadCapacityEntryDto[] ArchetypeCapacity { get; set; } = Array.Empty<WorkloadCapacityEntryDto>();
}

/// <summary>Single archetype capacity entry deserialized from JSON.</summary>
internal sealed class WorkloadCapacityEntryDto
{
    /// <summary>Archetype identifier the capacity applies to.</summary>
    public string ArchetypeId { get; set; } = "";
    /// <summary>Maximum concurrent active tasks for the archetype (clamped to 1–10 on load).</summary>
    public int    Capacity    { get; set; } = 3;
}

// -- System --------------------------------------------------------------------

/// <summary>
/// PreUpdate phase. Attaches <see cref="WorkloadComponent"/> to every NPC that has
/// <see cref="NpcArchetypeComponent"/> but no workload state yet. Capacity is loaded
/// from <c>archetype-workload-capacity.json</c>; defaults to 3 when unknown.
/// </summary>
/// <remarks>
/// Reads: <see cref="NpcTag"/>, <see cref="NpcArchetypeComponent"/>.<br/>
/// Writes: <see cref="WorkloadComponent"/> (single writer at attach time; idempotent).<br/>
/// Phase: PreUpdate, before any system that reads workload state.
/// </remarks>
public class WorkloadInitializerSystem : ISystem
{
    private readonly IReadOnlyDictionary<string, int> _capacities;

    /// <summary>Constructs the initializer with a pre-loaded capacities dictionary.</summary>
    /// <param name="capacities">Map of archetype id → max concurrent task capacity.</param>
    public WorkloadInitializerSystem(IReadOnlyDictionary<string, int> capacities)
    {
        _capacities = capacities;
    }

    /// <summary>Per-tick idempotent attach pass.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds, unused).</param>
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

    // -- Static helpers --------------------------------------------------------

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
