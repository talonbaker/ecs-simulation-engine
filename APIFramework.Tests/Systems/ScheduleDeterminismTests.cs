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
/// AT-09: ScheduleSystem + ActionSelectionSystem with the same seed produce a byte-identical
/// intent stream across 5000 ticks; different seeds produce different streams.
/// </summary>
public class ScheduleDeterminismTests
{
    private const int Ticks = 5000;

    private static ActionSelectionConfig DefaultCfg() => new()
    {
        DriveCandidateThreshold        = 60,
        IdleScoreFloor                 = 0.20,
        InversionStakeThreshold        = 0.55,
        InversionInhibitionThreshold   = 0.50,
        SuppressionGiveUpFactor        = 0.30,
        SuppressionEpsilon             = 0.10,
        SuppressionEventMagnitudeScale = 5,
        PersonalityTieBreakWeight      = 0.05,
        MaxCandidatesPerTick           = 32,
        AvoidStandoffDistance          = 4
    };

    private static ScheduleConfig SchedCfg() => new()
    {
        ScheduleAnchorBaseWeight     = 0.30,
        ScheduleLingerThresholdCells = 2.0f
    };

    private static GridSpatialIndex MakeSpatial() =>
        new(new SpatialConfig { CellSizeTiles = 4, WorldSize = new() { Width = 64, Height = 64 } });

    private static List<(int tick, int npcId, IntendedActionKind kind, int target, DialogContextValue ctx)>
        RunAndCollect(int seed, int ticks)
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var queue   = new WillpowerEventQueue();
        var clock   = new SimulationClock();

        // Anchor entity for schedule resolution
        var anchor = em.CreateEntity();
        anchor.Add(new NamedAnchorComponent { Tag = "test-desk" });
        anchor.Add(new PositionComponent { X = 15f, Y = 0f, Z = 5f });

        // NPC A: elevated drives + schedule
        var blocks = new List<ScheduleBlock>
        {
            new( 6.0f, 17.0f, "test-desk", ScheduleActivityKind.AtDesk),
            new(17.0f,  6.0f, "test-desk", ScheduleActivityKind.Sleeping),
        };

        var npcA = em.CreateEntity();
        npcA.Add(new NpcTag());
        npcA.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npcA.Add(new WillpowerComponent(60, 60));
        npcA.Add(new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = 72, Baseline = 72 },
            Loneliness = new DriveValue { Current = 65, Baseline = 65 },
        });
        npcA.Add(new InhibitionsComponent(new[]
        {
            new Inhibition(InhibitionClass.Confrontation, 40, InhibitionAwareness.Known)
        }));
        npcA.Add(new ScheduleComponent { Blocks = blocks });
        npcA.Add(new CurrentScheduleBlockComponent { ActiveBlockIndex = -1, AnchorEntityId = Guid.Empty });
        spatial.Register(npcA, 5, 5);

        // NPC B: nearby target for drive candidates
        var npcB = em.CreateEntity();
        npcB.Add(new NpcTag());
        npcB.Add(new PositionComponent { X = 6f, Y = 0f, Z = 5f });
        npcB.Add(new WillpowerComponent(80, 80));
        npcB.Add(new SocialDrivesComponent
        {
            Affection = new DriveValue { Current = 70, Baseline = 70 },
        });
        npcB.Add(new InhibitionsComponent(Array.Empty<Inhibition>()));
        spatial.Register(npcB, 6, 5);

        var schedSys = new ScheduleSystem(clock);
        var actSys   = new ActionSelectionSystem(spatial, new EntityRoomMembership(), queue,
            new SeededRandom(seed), DefaultCfg(), SchedCfg(), em);

        var results = new List<(int tick, int npcId, IntendedActionKind kind, int target, DialogContextValue ctx)>();
        int idA = WillpowerSystem.EntityIntId(npcA);
        int idB = WillpowerSystem.EntityIntId(npcB);

        for (int t = 0; t < ticks; t++)
        {
            clock.TimeScale = 1f;
            clock.Tick(1f);
            schedSys.Update(em, 1f);
            actSys.Update(em, 1f);

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
    public void AT09_SameSeed_ProducesByteIdenticalIntentStream()
    {
        var run1 = RunAndCollect(seed: 12345, ticks: Ticks);
        var run2 = RunAndCollect(seed: 12345, ticks: Ticks);

        Assert.Equal(run1.Count, run2.Count);
        for (int i = 0; i < run1.Count; i++)
            Assert.Equal(run1[i], run2[i]);
    }

    [Fact]
    public void AT09_DifferentSeeds_ProduceDifferentStreams()
    {
        var run1 = RunAndCollect(seed: 11111, ticks: Ticks);
        var run2 = RunAndCollect(seed: 99999, ticks: Ticks);

        bool differs = false;
        for (int i = 0; i < Math.Min(run1.Count, run2.Count) && !differs; i++)
            if (run1[i] != run2[i]) differs = true;

        Assert.True(differs, "Expected different intent streams for different seeds");
    }
}
