using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-10: Two runs with the same seed over 5000 ticks produce byte-identical
/// IntendedActionComponent snapshots at every tick boundary.
/// Different seeds produce different streams.
/// </summary>
public class ActionSelectionDeterminismTests
{
    private const int Ticks = 5000;

    private static ActionSelectionConfig DefaultCfg() => new()
    {
        DriveCandidateThreshold      = 60,
        IdleScoreFloor               = 0.20,
        InversionStakeThreshold      = 0.55,
        InversionInhibitionThreshold = 0.50,
        SuppressionGiveUpFactor      = 0.30,
        SuppressionEpsilon           = 0.10,
        SuppressionEventMagnitudeScale = 5,
        PersonalityTieBreakWeight    = 0.05,
        MaxCandidatesPerTick         = 32,
        AvoidStandoffDistance        = 4
    };

    private static GridSpatialIndex MakeSpatial() =>
        new(new SpatialConfig { CellSizeTiles = 4, WorldSize = new() { Width = 64, Height = 64 } });

    /// <summary>
    /// Builds a small world (2 NPCs) and runs the action-selection system for `ticks` ticks,
    /// returning the sequence of (tick, npcEntityId, Kind, TargetEntityId, Context) tuples.
    /// </summary>
    private static List<(int tick, int npcId, IntendedActionKind kind, int target, DialogContextValue ctx)>
        RunAndCollect(int seed, int ticks)
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();

        // NPC A: high irritation, moderate willpower
        var npcA = em.CreateEntity();
        npcA.Add(new NpcTag());
        npcA.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npcA.Add(new WillpowerComponent(60, 60));
        npcA.Add(new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = 75, Baseline = 75 },
            Loneliness = new DriveValue { Current = 65, Baseline = 65 },
        });
        npcA.Add(new InhibitionsComponent(new[]
        {
            new Inhibition(InhibitionClass.Confrontation, 40, InhibitionAwareness.Known)
        }));
        spatial.Register(npcA, 5, 5);

        // NPC B: in proximity range of A (1 tile away)
        var npcB = em.CreateEntity();
        npcB.Add(new NpcTag());
        npcB.Add(new PositionComponent { X = 6f, Y = 0f, Z = 5f });
        npcB.Add(new WillpowerComponent(80, 80));
        npcB.Add(new SocialDrivesComponent
        {
            Affection  = new DriveValue { Current = 70, Baseline = 70 },
        });
        npcB.Add(new InhibitionsComponent(Array.Empty<Inhibition>()));
        spatial.Register(npcB, 6, 5);

        var sys = new ActionSelectionSystem(spatial, new EntityRoomMembership(), queue,
            new SeededRandom(seed), DefaultCfg(), em);

        var results = new List<(int tick, int npcId, IntendedActionKind kind, int target, DialogContextValue ctx)>();
        int idA = WillpowerSystem.EntityIntId(npcA);
        int idB = WillpowerSystem.EntityIntId(npcB);

        for (int t = 0; t < ticks; t++)
        {
            sys.Update(em, 1f);

            foreach (var npc in new[] { npcA, npcB })
            {
                if (npc.Has<IntendedActionComponent>())
                {
                    var intent = npc.Get<IntendedActionComponent>();
                    results.Add((t, WillpowerSystem.EntityIntId(npc),
                        intent.Kind, intent.TargetEntityId, intent.Context));
                }
            }
        }

        return results;
    }

    [Fact]
    public void AT10_SameSeed_ProducesByteIdenticalIntentStream()
    {
        var run1 = RunAndCollect(seed: 12345, ticks: Ticks);
        var run2 = RunAndCollect(seed: 12345, ticks: Ticks);

        Assert.Equal(run1.Count, run2.Count);
        for (int i = 0; i < run1.Count; i++)
            Assert.Equal(run1[i], run2[i]);
    }

    [Fact]
    public void AT10_DifferentSeeds_ProduceDifferentStreams()
    {
        var run1 = RunAndCollect(seed: 11111, ticks: Ticks);
        var run2 = RunAndCollect(seed: 99999, ticks: Ticks);

        // At least one tick should differ (probability of identical is vanishingly small).
        bool differs = false;
        for (int i = 0; i < Math.Min(run1.Count, run2.Count) && !differs; i++)
            if (run1[i] != run2[i]) differs = true;

        Assert.True(differs, "Expected different intent streams for different seeds");
    }
}
