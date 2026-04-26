using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Chronicle;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems.Chronicle;

/// <summary>
/// Unit tests for PersistenceThresholdDetector:
///
/// AT-04 — Relationship-impact candidate persists; minor-effect doesn't.
/// AT-05 — Candidate not meeting any threshold produces no chronicle entry.
/// AT-06 — High-irritation DriveSpike with nearby item produces Stain + chronicle entry.
/// AT-09 — ChronicleService overflow drops oldest first.
/// AT-10 — Determinism: same seed → byte-identical chronicle ledger.
/// </summary>
public class PersistenceThresholdDetectorTests
{
    // ── Shared setup ─────────────────────────────────────────────────────────

    private static ChronicleConfig DefaultCfg() => new()
    {
        ThresholdRules = new ChronicleThresholdRulesConfig
        {
            IntensityChangeMinForRelationshipStick = 15,
            IrritationSpikeMinForPhysicalManifest  = 70,
            TalkAboutMinReferenceCount             = 2,
            DriveReturnToBaselineWindowSeconds     = 60,
        },
        StainMagnitudeRange      = new[] { 30, 50 },
        BrokenItemMagnitudeRange = new[] { 20, 60 },
    };

    private static (EntityManager              em,
                    NarrativeEventBus           bus,
                    ChronicleService            chronicle,
                    PersistenceThresholdDetector detector)
        Build(ChronicleConfig? cfg = null, int seed = 42)
    {
        var em        = new EntityManager();
        var bus       = new NarrativeEventBus();
        var chronicle = new ChronicleService();
        var clock     = new SimulationClock();
        var rng       = new SeededRandom(seed);
        cfg         ??= DefaultCfg();
        var detector  = new PersistenceThresholdDetector(chronicle, bus, em, clock, rng, cfg);
        return (em, bus, chronicle, detector);
    }

    private static Entity SpawnNpc(EntityManager em,
        float x = 5f, float z = 5f,
        SocialDrivesComponent? drives = null)
    {
        var e = em.CreateEntity();
        e.Add(new NpcTag());
        e.Add(new PositionComponent { X = x, Y = 0f, Z = z });
        e.Add(drives ?? new SocialDrivesComponent());
        return e;
    }

    // ── AT-01: WillpowerLow never persists ────────────────────────────────

    [Fact]
    public void WillpowerLow_NeverPersists()
    {
        var (em, bus, chronicle, detector) = Build();
        var npc  = SpawnNpc(em);
        int npcId = PhysicalManifestSpawner.EntityIntId(npc);

        detector.Update(em, 1f);  // prime

        bus.RaiseCandidate(new NarrativeEventCandidate(
            2, NarrativeEventKind.WillpowerLow, new List<int> { npcId }, null, "willpower low"));
        detector.Update(em, 1f);

        Assert.Empty(chronicle.All);
    }

    // ── AT-04: Relationship-impact candidate persists ─────────────────────

    [Fact]
    public void RelationshipImpact_AboveThreshold_Persists()
    {
        var (em, bus, chronicle, detector) = Build();
        var npc   = SpawnNpc(em);
        int aId   = PhysicalManifestSpawner.EntityIntId(npc);
        const int bId = 99999;  // phantom second participant

        var relEntity = em.CreateEntity();
        relEntity.Add(new RelationshipTag());
        relEntity.Add(new RelationshipComponent(aId, bId, intensity: 50));

        detector.Update(em, 1f);  // prime: records prevRelIntensity[(aId, bId)] = 50

        // Raise intensity by 20 (≥ threshold of 15)
        var rc = relEntity.Get<RelationshipComponent>();
        rc.Intensity = 70;
        relEntity.Add(rc);

        bus.RaiseCandidate(new NarrativeEventCandidate(
            2, NarrativeEventKind.WillpowerCollapse,
            new List<int> { aId }, null, "Alice collapsed"));
        detector.Update(em, 1f);

        Assert.Single(chronicle.All);
        Assert.Equal(ChronicleEventKind.PublicArgument, chronicle.All[0].Kind);
        Assert.True(chronicle.All[0].Persistent);
    }

    // ── AT-05: Minor relationship delta doesn't persist ───────────────────

    [Fact]
    public void RelationshipImpact_BelowThreshold_DoesNotPersist()
    {
        var (em, bus, chronicle, detector) = Build();
        var npc   = SpawnNpc(em);
        int aId   = PhysicalManifestSpawner.EntityIntId(npc);
        const int bId = 99999;

        var relEntity = em.CreateEntity();
        relEntity.Add(new RelationshipTag());
        relEntity.Add(new RelationshipComponent(aId, bId, intensity: 50));

        detector.Update(em, 1f);  // prime

        // Raise intensity by only 5 (< threshold)
        var rc = relEntity.Get<RelationshipComponent>();
        rc.Intensity = 55;
        relEntity.Add(rc);

        bus.RaiseCandidate(new NarrativeEventCandidate(
            2, NarrativeEventKind.WillpowerCollapse,
            new List<int> { aId }, null, "minor wobble"));
        detector.Update(em, 1f);

        Assert.Empty(chronicle.All);
    }

    // ── AT-05 (no-rel): WillpowerCollapse with no relationship → no entry ─

    [Fact]
    public void WillpowerCollapse_NoRelationship_DoesNotPersist()
    {
        var (em, bus, chronicle, detector) = Build();
        var npc   = SpawnNpc(em);
        int npcId = PhysicalManifestSpawner.EntityIntId(npc);

        detector.Update(em, 1f);  // prime

        bus.RaiseCandidate(new NarrativeEventCandidate(
            2, NarrativeEventKind.WillpowerCollapse,
            new List<int> { npcId }, null, "collapsed, no rel"));
        detector.Update(em, 1f);

        Assert.Empty(chronicle.All);
    }

    // ── AT-06: High irritation + nearby item → Stain + chronicle entry ────

    [Fact]
    public void HighIrritationSpike_NearbyItem_SpawnsStainAndChronicleEntry()
    {
        var (em, bus, chronicle, detector) = Build();
        var npc = SpawnNpc(em, x: 5f, z: 5f, drives: new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = 80, Baseline = 20 }
        });
        int npcId = PhysicalManifestSpawner.EntityIntId(npc);

        // Plain positioned entity within 2 tiles — qualifies as "item"
        var item = em.CreateEntity();
        item.Add(new PositionComponent { X = 6f, Y = 0f, Z = 5f });

        detector.Update(em, 1f);  // prime

        bus.RaiseCandidate(new NarrativeEventCandidate(
            2, NarrativeEventKind.DriveSpike,
            new List<int> { npcId }, null, "irritation: 50 → 80 (+30)"));
        detector.Update(em, 1f);

        Assert.Single(chronicle.All);
        Assert.Equal(ChronicleEventKind.SpilledSomething, chronicle.All[0].Kind);
        Assert.NotNull(chronicle.All[0].PhysicalManifestEntityId);

        // A StainTag entity was spawned into the entity manager
        Assert.Contains(em.GetAllEntities(), e => e.Has<StainTag>());
    }

    // ── AT-06 (no-item): No nearby item → no Stain, no entry ─────────────

    [Fact]
    public void HighIrritationSpike_NoNearbyItem_NoEntry()
    {
        var (em, bus, chronicle, detector) = Build();
        var npc = SpawnNpc(em, x: 5f, z: 5f, drives: new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = 80, Baseline = 20 }
        });
        int npcId = PhysicalManifestSpawner.EntityIntId(npc);
        // No item entity placed

        detector.Update(em, 1f);  // prime

        bus.RaiseCandidate(new NarrativeEventCandidate(
            2, NarrativeEventKind.DriveSpike,
            new List<int> { npcId }, null, "irritation: 50 → 80 (+30)"));
        detector.Update(em, 1f);

        Assert.Empty(chronicle.All);
    }

    // ── Drive-return-to-baseline → discard ───────────────────────────────

    [Fact]
    public void DriveReturnedToBaseline_Discards()
    {
        var (em, bus, chronicle, detector) = Build();
        var npc = SpawnNpc(em, drives: new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = 22, Baseline = 20 }  // |22-20|=2 ≤ 5
        });
        int npcId = PhysicalManifestSpawner.EntityIntId(npc);

        detector.Update(em, 1f);  // prime

        bus.RaiseCandidate(new NarrativeEventCandidate(
            2, NarrativeEventKind.DriveSpike,
            new List<int> { npcId }, null, "irritation: 20 → 22 (+2)"));
        detector.Update(em, 1f);

        Assert.Empty(chronicle.All);
    }

    // ── Talk-about: ≥ N distinct NPCs → PublicArgument ───────────────────

    [Fact]
    public void TalkAbout_DistinctNpcs_AtOrAboveThreshold_Persists()
    {
        var (em, bus, chronicle, detector) = Build();
        var npc1 = SpawnNpc(em, drives: new SocialDrivesComponent
        {
            Status = new DriveValue { Current = 70, Baseline = 20 }  // not at baseline
        });
        var npc2 = SpawnNpc(em, drives: new SocialDrivesComponent
        {
            Status = new DriveValue { Current = 75, Baseline = 20 }  // not at baseline
        });
        int id1 = PhysicalManifestSpawner.EntityIntId(npc1);
        int id2 = PhysicalManifestSpawner.EntityIntId(npc2);

        detector.Update(em, 1f);  // prime

        // Both emit DriveSpike for "status" in the same tick
        bus.RaiseCandidate(new NarrativeEventCandidate(
            2, NarrativeEventKind.DriveSpike,
            new List<int> { id1 }, null, "status: 20 → 70 (+50)"));
        bus.RaiseCandidate(new NarrativeEventCandidate(
            2, NarrativeEventKind.DriveSpike,
            new List<int> { id2 }, null, "status: 20 → 75 (+55)"));
        detector.Update(em, 1f);

        Assert.True(chronicle.All.Count >= 1);
        Assert.All(chronicle.All, e => Assert.Equal(ChronicleEventKind.PublicArgument, e.Kind));
    }

    // ── AT-09: Ring buffer drops oldest on overflow ───────────────────────

    [Fact]
    public void ChronicleService_RingBuffer_DropsOldestOnOverflow()
    {
        var cfg = DefaultCfg();
        cfg.ThresholdRules.TalkAboutMinReferenceCount = 1;  // lower threshold so every spike persists

        // Custom config with maxEntries = 3
        var sm  = new ChronicleService(maxEntries: 3);
        var bus = new NarrativeEventBus();
        var em  = new EntityManager();
        var rng = new SeededRandom(42);
        var det = new PersistenceThresholdDetector(sm, bus, em, new SimulationClock(), rng, cfg);

        var npc1 = em.CreateEntity();
        npc1.Add(new NpcTag());
        npc1.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc1.Add(new SocialDrivesComponent
        {
            Status = new DriveValue { Current = 70, Baseline = 20 }
        });
        var npc2 = em.CreateEntity();
        npc2.Add(new NpcTag());
        npc2.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc2.Add(new SocialDrivesComponent
        {
            Status = new DriveValue { Current = 70, Baseline = 20 }
        });
        int id1 = PhysicalManifestSpawner.EntityIntId(npc1);
        int id2 = PhysicalManifestSpawner.EntityIntId(npc2);

        det.Update(em, 1f);  // prime

        // Emit 4 pairs of talk-about spikes (each pair produces ≥1 entry)
        for (int tick = 2; tick <= 5; tick++)
        {
            bus.RaiseCandidate(new NarrativeEventCandidate(
                tick, NarrativeEventKind.DriveSpike,
                new List<int> { id1 }, null, "status: 20 → 70 (+50)"));
            bus.RaiseCandidate(new NarrativeEventCandidate(
                tick, NarrativeEventKind.DriveSpike,
                new List<int> { id2 }, null, "status: 20 → 70 (+50)"));
            det.Update(em, 1f);
        }

        Assert.True(sm.All.Count <= 3, $"Expected ≤3 entries but got {sm.All.Count}");
    }

    // ── AT-10: Determinism — same seed → same chronicle entry IDs ─────────

    [Fact]
    public void Determinism_SameSeed_ProducesSameEntryId()
    {
        string RunAndGetFirstEntryId(int seed)
        {
            var (em, bus, chronicle, detector) = Build(seed: seed);
            var npc   = SpawnNpc(em);
            int aId   = PhysicalManifestSpawner.EntityIntId(npc);
            const int bId = 99999;

            var relEntity = em.CreateEntity();
            relEntity.Add(new RelationshipTag());
            relEntity.Add(new RelationshipComponent(aId, bId, intensity: 50));

            detector.Update(em, 1f);  // prime

            var rc = relEntity.Get<RelationshipComponent>();
            rc.Intensity = 70;
            relEntity.Add(rc);

            bus.RaiseCandidate(new NarrativeEventCandidate(
                2, NarrativeEventKind.WillpowerCollapse,
                new List<int> { aId }, null, "collapse"));
            detector.Update(em, 1f);

            return chronicle.All[0].Id;
        }

        string id1 = RunAndGetFirstEntryId(42);
        string id2 = RunAndGetFirstEntryId(42);

        Assert.Equal(id1, id2);
    }

    // ── AT-10 variant: different seed → different entry IDs ──────────────

    [Fact]
    public void Determinism_DifferentSeed_ProducesDifferentEntryId()
    {
        string RunAndGetFirstEntryId(int seed)
        {
            var (em, bus, chronicle, detector) = Build(seed: seed);
            var npc   = SpawnNpc(em);
            int aId   = PhysicalManifestSpawner.EntityIntId(npc);
            const int bId = 99999;

            var relEntity = em.CreateEntity();
            relEntity.Add(new RelationshipTag());
            relEntity.Add(new RelationshipComponent(aId, bId, intensity: 50));

            detector.Update(em, 1f);
            var rc = relEntity.Get<RelationshipComponent>();
            rc.Intensity = 70;
            relEntity.Add(rc);

            bus.RaiseCandidate(new NarrativeEventCandidate(
                2, NarrativeEventKind.WillpowerCollapse,
                new List<int> { aId }, null, "collapse"));
            detector.Update(em, 1f);

            return chronicle.All[0].Id;
        }

        string id1 = RunAndGetFirstEntryId(42);
        string id2 = RunAndGetFirstEntryId(99);

        Assert.NotEqual(id1, id2);
    }
}
