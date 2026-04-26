using System.IO;
using APIFramework.Components;
using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Bootstrap;

/// <summary>
/// Integration tests for the cast generator end-to-end boot path.
///
/// AT-09 — Load office-starter.json, run cast generator, tick 100 ticks without error.
///         Asserts ≥5 NPCs spawned and ≥5 relationships seeded.
/// </summary>
public class CastGeneratorIntegrationTests
{
    private const float DeltaTime = 1f / 60f;

    private static readonly string StarterJsonPath = FindStarterJson();

    // ── AT-09: full integration boot + 100 ticks ─────────────────────────────

    [Fact]
    public void Bootstrapper_WithWorldDefinition_SpawnsNpcsAndRelationships()
    {
        using var tmp = new TempCopy(StarterJsonPath);

        var sim = new SimulationBootstrapper(
            configPath:          "SimConfig.json",
            humanCount:          0,
            worldDefinitionPath: tmp.Path);

        Assert.NotNull(sim.SpawnedNpcs);
        Assert.True(sim.SpawnedNpcs!.Count >= 5,
            $"Expected ≥5 spawned NPCs, got {sim.SpawnedNpcs.Count}");

        Assert.NotNull(sim.SeededRelationships);
        Assert.True(sim.SeededRelationships!.Count >= 5,
            $"Expected ≥5 seeded relationships, got {sim.SeededRelationships.Count}");
    }

    [Fact]
    public void Bootstrapper_WithWorldDefinition_AllNpcsHaveRequiredComponents()
    {
        using var tmp = new TempCopy(StarterJsonPath);

        var sim = new SimulationBootstrapper(
            configPath:          "SimConfig.json",
            humanCount:          0,
            worldDefinitionPath: tmp.Path);

        Assert.NotNull(sim.SpawnedNpcs);
        foreach (var npc in sim.SpawnedNpcs!)
        {
            Assert.True(npc.Has<NpcTag>(),              $"NPC {npc.Id} missing NpcTag");
            Assert.True(npc.Has<SocialDrivesComponent>(), $"NPC {npc.Id} missing SocialDrivesComponent");
            Assert.True(npc.Has<WillpowerComponent>(),    $"NPC {npc.Id} missing WillpowerComponent");
            Assert.True(npc.Has<PersonalityComponent>(),  $"NPC {npc.Id} missing PersonalityComponent");
            Assert.True(npc.Has<InhibitionsComponent>(),  $"NPC {npc.Id} missing InhibitionsComponent");
            Assert.True(npc.Has<NpcArchetypeComponent>(), $"NPC {npc.Id} missing NpcArchetypeComponent");
        }
    }

    [Fact]
    public void Bootstrapper_WithWorldDefinition_NoNpcSlotEntitiesRemain()
    {
        using var tmp = new TempCopy(StarterJsonPath);

        var sim = new SimulationBootstrapper(
            configPath:          "SimConfig.json",
            humanCount:          0,
            worldDefinitionPath: tmp.Path);

        var remaining = sim.EntityManager.Query<NpcSlotTag>().Count();
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void Bootstrapper_WithWorldDefinition_Ticks100WithoutError()
    {
        using var tmp = new TempCopy(StarterJsonPath);

        var sim = new SimulationBootstrapper(
            configPath:          "SimConfig.json",
            humanCount:          0,
            worldDefinitionPath: tmp.Path);

        var ex = Record.Exception(() =>
        {
            for (int i = 0; i < 100; i++)
                sim.Engine.Update(DeltaTime);
        });

        Assert.Null(ex);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FindStarterJson()
    {
        var dir = new System.IO.DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(
                dir.FullName, "docs", "c2-content", "world-definitions", "office-starter.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate office-starter.json.");
    }

    private sealed class TempCopy : System.IDisposable
    {
        public string Path { get; }
        public TempCopy(string source)
        {
            Path = System.IO.Path.GetTempFileName() + ".json";
            File.Copy(source, Path, overwrite: true);
        }
        public void Dispose()
        {
            if (File.Exists(Path)) File.Delete(Path);
        }
    }
}
