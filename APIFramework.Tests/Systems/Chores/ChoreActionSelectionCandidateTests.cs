using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Chores;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Chores;

/// <summary>AT-04: ActionSelectionSystem emits a ChoreWork candidate when NPC is assigned a pending chore.</summary>
public class ChoreActionSelectionCandidateTests
{
    private const string BiasJson = @"{
        ""schemaVersion"": ""0.1.0"",
        ""biases"": {
            ""the-newbie"": { ""cleanMicrowave"": 0.95 }
        }
    }";

    private static ActionSelectionConfig DefaultActionCfg() => new()
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
        AvoidStandoffDistance        = 4,
    };

    [Fact]
    public void AssignedNpc_WithAcceptableBias_GetsChoreWorkCandidate()
    {
        var em      = new EntityManager();
        var spatial = new GridSpatialIndex(new SpatialConfig
            { CellSizeTiles = 4, WorldSize = new() { Width = 64, Height = 64 } });
        var queue   = new WillpowerEventQueue();
        var bus     = new NarrativeEventBus();
        var cfg     = new ChoreConfig { MinChoreAcceptanceBias = 0.20, ChoreActionBaseWeight = 0.35 };
        var table   = ChoreAcceptanceBiasTable.ParseJson(BiasJson);

        var sys = new ActionSelectionSystem(
            spatial, new EntityRoomMembership(), queue, new SeededRandom(42),
            DefaultActionCfg(), new ScheduleConfig(), em,
            choreCfg: cfg, choreBiasTable: table, narrativeBus: bus);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialDrivesComponent());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });
        npc.Add(new WillpowerComponent(50, 50));
        npc.Add(new InhibitionsComponent(Array.Empty<Inhibition>()));
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        spatial.Register(npc, 5, 5);

        var choreEntity = em.CreateEntity();
        choreEntity.Add(new ChoreComponent
        {
            Kind              = ChoreKind.CleanMicrowave,
            CompletionLevel   = 0.0f,
            CurrentAssigneeId = npc.Id,
        });

        sys.Update(em, 1f);

        Assert.True(npc.Has<IntendedActionComponent>());
        Assert.Equal(IntendedActionKind.ChoreWork, npc.Get<IntendedActionComponent>().Kind);
    }

    [Fact]
    public void CompletedChore_DoesNotProduceChoreWorkCandidate()
    {
        var em      = new EntityManager();
        var spatial = new GridSpatialIndex(new SpatialConfig
            { CellSizeTiles = 4, WorldSize = new() { Width = 64, Height = 64 } });
        var queue   = new WillpowerEventQueue();
        var bus     = new NarrativeEventBus();
        var cfg     = new ChoreConfig { MinChoreAcceptanceBias = 0.20, ChoreActionBaseWeight = 0.35 };
        var table   = ChoreAcceptanceBiasTable.ParseJson(BiasJson);

        var sys = new ActionSelectionSystem(
            spatial, new EntityRoomMembership(), queue, new SeededRandom(42),
            DefaultActionCfg(), new ScheduleConfig(), em,
            choreCfg: cfg, choreBiasTable: table, narrativeBus: bus);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialDrivesComponent());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });
        npc.Add(new WillpowerComponent(50, 50));
        npc.Add(new InhibitionsComponent(Array.Empty<Inhibition>()));
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        spatial.Register(npc, 5, 5);

        var choreEntity = em.CreateEntity();
        choreEntity.Add(new ChoreComponent
        {
            Kind              = ChoreKind.CleanMicrowave,
            CompletionLevel   = 1.0f,    // already complete
            CurrentAssigneeId = npc.Id,
        });

        sys.Update(em, 1f);

        // Should fall back to Idle, not ChoreWork
        Assert.True(npc.Has<IntendedActionComponent>());
        Assert.NotEqual(IntendedActionKind.ChoreWork, npc.Get<IntendedActionComponent>().Kind);
    }

    [Fact]
    public void UnassignedNpc_DoesNotGetChoreWorkCandidate()
    {
        var em      = new EntityManager();
        var spatial = new GridSpatialIndex(new SpatialConfig
            { CellSizeTiles = 4, WorldSize = new() { Width = 64, Height = 64 } });
        var queue   = new WillpowerEventQueue();
        var bus     = new NarrativeEventBus();
        var cfg     = new ChoreConfig { MinChoreAcceptanceBias = 0.20, ChoreActionBaseWeight = 0.35 };
        var table   = ChoreAcceptanceBiasTable.ParseJson(BiasJson);

        var sys = new ActionSelectionSystem(
            spatial, new EntityRoomMembership(), queue, new SeededRandom(42),
            DefaultActionCfg(), new ScheduleConfig(), em,
            choreCfg: cfg, choreBiasTable: table, narrativeBus: bus);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialDrivesComponent());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });
        npc.Add(new WillpowerComponent(50, 50));
        npc.Add(new InhibitionsComponent(Array.Empty<Inhibition>()));
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        spatial.Register(npc, 5, 5);

        var choreEntity = em.CreateEntity();
        choreEntity.Add(new ChoreComponent
        {
            Kind              = ChoreKind.CleanMicrowave,
            CompletionLevel   = 0.0f,
            CurrentAssigneeId = Guid.Empty,  // not assigned to this NPC
        });

        sys.Update(em, 1f);

        Assert.True(npc.Has<IntendedActionComponent>());
        Assert.NotEqual(IntendedActionKind.ChoreWork, npc.Get<IntendedActionComponent>().Kind);
    }
}
