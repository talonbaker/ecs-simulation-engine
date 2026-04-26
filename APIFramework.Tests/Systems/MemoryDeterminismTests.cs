using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-11 — Determinism: 5000-tick run, two seeds with the same world: byte-identical
///         memory state across runs.
/// </summary>
public class MemoryDeterminismTests
{
    private static List<string> RunSim(int seed)
    {
        var cfg = new SimConfig();
        cfg.Narrative.DriveSpikeThreshold    = 1;
        cfg.Narrative.WillpowerDropThreshold = 1;
        cfg.Narrative.WillpowerLowThreshold  = 99;

        var sim = new SimulationBootstrapper(
            new InMemoryConfigProvider(cfg), humanCount: 2, seed: seed);

        foreach (var e in sim.EntityManager.Query<HumanTag>().ToList())
        {
            EntityTemplates.WithSocial(e);
            EntityTemplates.WithProximity(e);
        }

        const int Ticks = 5000;
        for (int i = 0; i < Ticks; i++)
            sim.Engine.Update(1f / 60f);

        return CollectMemoryState(sim.EntityManager);
    }

    private static List<string> CollectMemoryState(EntityManager em)
    {
        var lines = new List<string>();

        foreach (var e in em.Query<RelationshipMemoryComponent>()
                             .OrderBy(e => e.Id))
        {
            var mem = e.Get<RelationshipMemoryComponent>().Recent;
            foreach (var m in mem)
                lines.Add(JsonSerializer.Serialize(new { m.Id, m.Tick, m.Kind, m.Persistent }));
        }

        foreach (var e in em.Query<PersonalMemoryComponent>()
                             .OrderBy(e => e.Id))
        {
            var mem = e.Get<PersonalMemoryComponent>().Recent;
            foreach (var m in mem)
                lines.Add(JsonSerializer.Serialize(new { m.Id, m.Tick, m.Kind, m.Persistent }));
        }

        return lines;
    }

    [Fact]
    public void FiveThousandTicks_SameSeed_ByteIdenticalMemoryState()
    {
        const int Seed = 42;

        var runA = RunSim(Seed);
        var runB = RunSim(Seed);

        Assert.Equal(runA.Count, runB.Count);
        for (int i = 0; i < runA.Count; i++)
            Assert.Equal(runA[i], runB[i]);
    }
}
