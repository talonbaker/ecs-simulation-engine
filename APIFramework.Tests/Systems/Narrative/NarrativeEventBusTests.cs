using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;
using Warden.Contracts;
using Xunit;

namespace APIFramework.Tests.Systems.Narrative;

/// <summary>
/// Unit/integration tests for NarrativeEventBus ordering and determinism.
/// AT-08: candidates fire in entity-id ascending order within a tick.
/// AT-09: two runs with the same seed produce a byte-identical candidate stream.
/// </summary>
public class NarrativeEventBusTests
{
    // -- Helpers ---------------------------------------------------------------

    private static (EntityManager em,
                    NarrativeEventBus bus,
                    NarrativeEventDetector detector)
        Build(NarrativeConfig cfg)
    {
        var em         = new EntityManager();
        var bus        = new NarrativeEventBus();
        var proxBus    = new ProximityEventBus();
        var membership = new EntityRoomMembership();
        var detector   = new NarrativeEventDetector(bus, proxBus, membership, cfg);
        return (em, bus, detector);
    }

    private static Entity SpawnNpc(EntityManager em, SocialDrivesComponent? drives = null)
    {
        var e = em.CreateEntity();
        e.Add(new NpcTag());
        e.Add(drives ?? new SocialDrivesComponent());
        e.Add(new WillpowerComponent(50, 50));
        return e;
    }

    private static List<NarrativeEventCandidate> Collect(
        NarrativeEventBus bus, Action tick)
    {
        var list = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += list.Add;
        tick();
        bus.OnCandidateEmitted -= list.Add;
        return list;
    }

    // -- AT-08: entity-id ascending order within a tick ------------------------

    [Fact]
    public void CandidatesWithinTick_EmittedInEntityIdAscendingOrder()
    {
        var cfg = new NarrativeConfig
        {
            DriveSpikeThreshold       = 1,  // fire on any 1-point change
            WillpowerDropThreshold    = 99,  // suppress willpower candidates
            WillpowerLowThreshold     = 0,   // suppress willpower-low candidates
            AbruptDepartureWindowTicks = 3,
            CandidateDetailMaxLength   = 280,
        };
        var (em, bus, detector) = Build(cfg);

        // npcA is created first → lower entity int ID, npcB second → higher
        var npcA = SpawnNpc(em, drives: new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = 10, Baseline = 10 }
        });
        var npcB = SpawnNpc(em, drives: new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = 10, Baseline = 10 }
        });

        detector.Update(em, 1f);  // prime both caches

        // Spike irritation by 5 on both simultaneously in the next tick
        var dA = npcA.Get<SocialDrivesComponent>();
        dA.Irritation.Current = 15;
        npcA.Add(dA);

        var dB = npcB.Get<SocialDrivesComponent>();
        dB.Irritation.Current = 15;
        npcB.Add(dB);

        var candidates = Collect(bus, () => detector.Update(em, 1f));

        Assert.Equal(2, candidates.Count);

        int idA = NarrativeEventDetector.EntityIntId(npcA);
        int idB = NarrativeEventDetector.EntityIntId(npcB);
        Assert.True(idA < idB,
            $"Test invariant broken: npcA id ({idA}) must be < npcB id ({idB}).");

        Assert.Equal(idA, candidates[0].ParticipantIds[0]);
        Assert.Equal(idB, candidates[1].ParticipantIds[0]);
    }

    // -- AT-09: 5000-tick determinism -----------------------------------------

    [Fact]
    public void SameSeed_FiveThousandTicks_ProducesByteIdenticalCandidateStream()
    {
        const int Ticks = 5000;
        const int Seed  = 42;

        List<string> RunSim()
        {
            var cfg = new SimConfig();
            cfg.Narrative.DriveSpikeThreshold = 1;  // fire on any drive change

            var sim = new SimulationBootstrapper(
                new InMemoryConfigProvider(cfg), humanCount: 2, seed: Seed);

            // Add NpcTag + social components so the detector has entities to watch
            foreach (var e in sim.EntityManager.Query<HumanTag>().ToList())
            {
                EntityTemplates.WithSocial(e);
                EntityTemplates.WithProximity(e);
            }

            var lines = new List<string>();
            sim.NarrativeBus.OnCandidateEmitted += c =>
                lines.Add(JsonSerializer.Serialize(c, JsonOptions.Wire));

            for (int i = 0; i < Ticks; i++)
                sim.Engine.Update(1f / 60f);

            return lines;
        }

        var runA = RunSim();
        var runB = RunSim();

        Assert.True(runA.Count > 0,
            $"Expected candidates in {Ticks} ticks with DriveSpikeThreshold=1.");
        Assert.Equal(runA.Count, runB.Count);
        for (int i = 0; i < runA.Count; i++)
            Assert.Equal(runA[i], runB[i]);
    }
}
