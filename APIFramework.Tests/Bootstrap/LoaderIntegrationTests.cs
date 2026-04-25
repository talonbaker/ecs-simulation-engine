using System;
using System.IO;
using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Bootstrap;

/// <summary>
/// Integration tests: SimulationBootstrapper boot path with --world-definition.
///
/// AT-09 — Bootstrapper with world-definition flag + starter file ticks without error for 100 ticks.
/// AT-10 — Without world-definition, the bootstrapper continues using default templates (existing tests pass).
/// </summary>
public class LoaderIntegrationTests
{
    private const float DeltaTime = 1f / 60f;

    // ── AT-09: world-definition boot path ticks cleanly ──────────────────────

    [Fact]
    public void Bootstrapper_WithWorldDefinition_TicksWithoutErrorFor100Ticks()
    {
        using var tmp = new TempCopy(StarterJsonPath);

        var sim = new SimulationBootstrapper(
            configPath:           "SimConfig.json",
            humanCount:           0,   // no default humans when loading from definition
            worldDefinitionPath:  tmp.Path);

        Assert.NotNull(sim.WorldLoadResult);
        Assert.True(sim.WorldLoadResult!.RoomCount >= 6,
            $"Expected ≥6 rooms after world-definition boot, got {sim.WorldLoadResult.RoomCount}");

        var ex = Record.Exception(() =>
        {
            for (int i = 0; i < 100; i++)
                sim.Engine.Update(DeltaTime);
        });

        Assert.Null(ex);
    }

    [Fact]
    public void Bootstrapper_WithWorldDefinition_InvariantViolations_AreFew()
    {
        using var tmp = new TempCopy(StarterJsonPath);

        var sim = new SimulationBootstrapper(
            configPath:           "SimConfig.json",
            humanCount:           0,
            worldDefinitionPath:  tmp.Path);

        for (int i = 0; i < 100; i++)
            sim.Engine.Update(DeltaTime);

        Assert.True(sim.Invariants.Violations.Count < 20,
            $"Unexpected invariant violations after 100 ticks with world-definition: {sim.Invariants.Violations.Count}");
    }

    // ── AT-10: default path still works without world-definition ─────────────

    [Fact]
    public void Bootstrapper_WithoutWorldDefinition_BootsNormally()
    {
        // No worldDefinitionPath → SpawnWorld called, WorldLoadResult is null.
        var sim = new SimulationBootstrapper(humanCount: 1);

        Assert.Null(sim.WorldLoadResult);

        var ex = Record.Exception(() =>
        {
            for (int i = 0; i < 100; i++)
                sim.Engine.Update(DeltaTime);
        });

        Assert.Null(ex);
    }

    [Fact]
    public void Bootstrapper_WithWorldDefinition_WorldLoadResult_PopulatedCorrectly()
    {
        using var tmp = new TempCopy(StarterJsonPath);

        var sim = new SimulationBootstrapper(
            configPath:          "SimConfig.json",
            humanCount:          0,
            worldDefinitionPath: tmp.Path);

        var lr = sim.WorldLoadResult!;
        Assert.True(lr.LightSourceCount >= 8,  $"Expected ≥8 light sources, got {lr.LightSourceCount}");
        Assert.True(lr.ApertureCount    >= 2,  $"Expected ≥2 apertures, got {lr.ApertureCount}");
        Assert.True(lr.NpcSlotCount     >= 5,  $"Expected ≥5 NPC slots, got {lr.NpcSlotCount}");
        Assert.Equal(19990101, lr.SeedUsed);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly string StarterJsonPath = FindStarterJson();

    private static string FindStarterJson()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(
                dir.FullName, "docs", "c2-content", "world-definitions", "office-starter.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate office-starter.json.");
    }

    // Copy to a temp path so the bootstrapper can pass it as a string path.
    private sealed class TempCopy : IDisposable
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
